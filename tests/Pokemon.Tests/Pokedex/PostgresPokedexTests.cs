using Pokemon.Domain.Common.Exceptions;
using Pokemon.Domain.Pokedex.Exceptions;
using Pokemon.Domain.Common.Persistence;
using System.Text.Json;
using Npgsql;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Persistence;
using Pokemon.Infrastructure.Persistence.Migrations;
using Pokemon.Tests.Battle;

namespace Pokemon.Tests.Pokedex;

[Trait("Category", "Postgres")]
public sealed class PostgresPokedexTests
{
    private static readonly BaseStats Stats = new(40, 50, 60, 70, 80, 90);
    /// <summary>Crea una base de pruebas aislada con las migraciones de partidas y Pokédex aplicadas.</summary>
    private static async Task<TestDatabase> Database()
    {
        var db = await TestDatabase.Create();
        try { await new PokedexDatabaseMigrator(new DatabaseConnectionFactory(db.Source)).Migrate(default); return db; }
        catch { await db.DisposeAsync(); throw; }
    }
    /// <summary>Serializa el catálogo de la sesión con orden estable para comparar estados entre operaciones.</summary>
    private static async Task<string> Snapshot(IReadRepositoryScope data) => JsonSerializer.Serialize(new
    {
        Moves = (await data.GetReader<IMoveReader>().ListAsync(new(), default)).OrderBy(value => value.Id),
        Species = (await data.GetReader<ISpeciesReader>().ListAsync(new(), default)).OrderBy(value => value.Id),
        Pokemon = (await data.GetReader<IOwnedPokemonReader>().ListAsync(new(), default)).OrderBy(value => value.Id)
    });

    /// <summary>Comprueba que repetir la migración no sobrescribe cambios ni restaura recursos eliminados.</summary>
    [PostgresFact]
    public async Task Seed_runs_once_and_never_overwrites_changes_or_resurrects_deletions()
    {
        await using var db = await Database();
        var store = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        await store.WriteAsync(async data =>
        {
            var move = (await data.GetReader<IMoveReader>().ListAsync(new(), default)).First();
            await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(move.Id, "Edited after seed", move.Power, move.Type), default);
            await data.GetRepository<IOwnedPokemonRepository>().DeleteAsync((await data.GetReader<IOwnedPokemonReader>().ListAsync(new(), default)).First().Id, default);
            return true;
        }, default);
        var before = await store.ReadAsync(Snapshot, default);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => new PokedexDatabaseMigrator(new DatabaseConnectionFactory(db.Source)).Migrate(default)));
        Assert.Equal(before, await store.ReadAsync(Snapshot, default));
        Assert.Equal(4, await store.ReadAsync(async data => (await data.GetReader<IOwnedPokemonReader>().ListAsync(new(), default)).Count, default));
    }

    /// <summary>Comprueba que los agregados se recuperan, actualizan y eliminan desde conexiones diferentes.</summary>
    [PostgresFact]
    public async Task Aggregates_round_trip_update_and_delete_across_new_connections()
    {
        await using var db = await Database();
        var store = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        var moves = Enumerable.Range(1, 4).Select(index => new CatalogMove(Guid.NewGuid(), $"Persisted {index}", index * 20, PokemonType.Fairy)).ToArray();
        var species = new Species(Guid.NewGuid(), "Persisted species", PokemonType.Fairy, Stats,
            moves.Select(move => new LearnableMove(move.Id, 1)));
        var pokemon = new OwnedPokemon(Guid.NewGuid(), species, "Persisted Pokemon", 20, 0, 40, moves.Reverse().Select(move => move.Id));
        await store.WriteAsync(async data =>
        {
            foreach (var move in moves) await data.GetRepository<IMoveRepository>().SaveAsync(move, default);
            await data.GetRepository<ISpeciesRepository>().SaveAsync(species, default);
            await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(pokemon, default);
            return true;
        }, default);
        var before = await store.ReadAsync(Snapshot, default);
        await using var reconnected = NpgsqlDataSource.Create(db.ConnectionString);
        var restored = new DatabaseUnitOfWork(new DatabaseConnectionFactory(reconnected));
        Assert.Equal(before, await restored.ReadAsync(Snapshot, default));
        await restored.WriteAsync(async data =>
        {
            var saved = (await data.GetReader<IOwnedPokemonReader>().FindAsync(pokemon.Id, default))!;
            Assert.Equal(pokemon.MoveIds, saved.MoveIds);
            await data.GetRepository<IOwnedPokemonRepository>().SaveAsync(new OwnedPokemon(saved.Id, species, "Updated", saved.Level, 12, 40, saved.MoveIds), default);
            return true;
        }, default);
        Assert.Equal(12, await store.ReadAsync(async data => (await data.GetReader<IOwnedPokemonReader>().FindAsync(pokemon.Id, default))!.CurrentHealth, default));
        await restored.WriteAsync(async data =>
        {
            await data.GetRepository<IOwnedPokemonRepository>().DeleteAsync(pokemon.Id, default);
            await data.GetRepository<ISpeciesRepository>().DeleteAsync(species.Id, default);
            foreach (var move in moves) await data.GetRepository<IMoveRepository>().DeleteAsync(move.Id, default);
            return true;
        }, default);
        Assert.Equal((21, 5, 5), await store.ReadAsync(async data => ((await data.GetReader<IMoveReader>().ListAsync(new(), default)).Count, (await data.GetReader<ISpeciesReader>().ListAsync(new(), default)).Count, (await data.GetReader<IOwnedPokemonReader>().ListAsync(new(), default)).Count), default));
    }

    /// <summary>Comprueba que fallos del callback, cancelación y errores SQL revierten toda la operación.</summary>
    [PostgresFact]
    public async Task Callback_failure_cancellation_and_database_failure_roll_back_all_changes()
    {
        await using var db = await Database();
        var store = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        var before = await store.ReadAsync(Snapshot, default);
        var move = new CatalogMove(Guid.NewGuid(), "Rollback", 40, PokemonType.Normal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync<int>(async data =>
        { await data.GetRepository<IMoveRepository>().SaveAsync(move, default); throw new InvalidOperationException(); }, default));
        using var cancelled = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(async data =>
        { await data.GetRepository<IMoveRepository>().SaveAsync(move, default); cancelled.Cancel(); return true; }, cancelled.Token));
        // The move INSERT succeeds, but the deferred foreign key fails at commit.
        await Assert.ThrowsAsync<PersistenceConflictException>(() => store.WriteAsync(async data =>
        {
            await data.GetRepository<IMoveRepository>().SaveAsync(move, default);
            await data.GetRepository<ISpeciesRepository>().SaveAsync(new Species(Guid.NewGuid(), "Invalid foreign key", PokemonType.Normal, Stats,
                [new LearnableMove(Guid.NewGuid(), 1)]), default);
            return true;
        }, default));
        Assert.Equal(before, await store.ReadAsync(Snapshot, default));
    }

    /// <summary>Comprueba que escritores de distintas instancias ven los nombres confirmados antes de intentar crear otro.</summary>
    [PostgresFact]
    public async Task Concurrent_instances_observe_latest_names_before_writing()
    {
        await using var db = await Database();
        await using var secondSource = NpgsqlDataSource.Create(db.ConnectionString);
        var repositories = new[] { new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source)), new DatabaseUnitOfWork(new DatabaseConnectionFactory(secondSource)) };
        var successes = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => repositories[index % 2].WriteAsync(async data =>
        {
            if (await data.GetReader<IMoveReader>().NameExistsAsync("Concurrent", Guid.Empty, default)) return false;
            await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Guid.NewGuid(), "Concurrent", 40, PokemonType.Normal), default);
            return true;
        }, default)));
        Assert.Single(successes, success => success);
        Assert.Single(await repositories[0].ReadAsync(async data => (await data.GetReader<IMoveReader>().ListAsync(new(), default)).Where(move => move.Name == "Concurrent").ToArray(), default));
    }

    /// <summary>Comprueba que una sesión terminada y una sesión de lectura no permiten escribir.</summary>
    [PostgresFact]
    public async Task Escaped_session_cannot_write_after_transaction_completion()
    {
        await using var db = await Database();
        var store = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        IRepositoryScope? escaped = null;
        await store.WriteAsync(data => { escaped = data; return Task.FromResult(true); }, default);
        var before = await store.ReadAsync(Snapshot, default);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => escaped!.GetRepository<IMoveRepository>().DeleteAsync(Guid.NewGuid(), default));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(async data =>
        {
            await ((IRepositoryScope)data).GetRepository<IOwnedPokemonRepository>().DeleteAsync(Guid.NewGuid(), default);
            return true;
        }, default));
        Assert.Contains("read session", error.Message);
        Assert.Equal(before, await store.ReadAsync(Snapshot, default));
    }

    /// <summary>Comprueba que una lectura repetible conserva su instantánea aunque otra transacción confirme cambios.</summary>
    [PostgresFact]
    public async Task Read_snapshot_does_not_mix_data_from_an_intervening_commit()
    {
        await using var db = await Database();
        var store = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        var before = await store.ReadAsync(Snapshot, default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = store.ReadAsync(async data =>
        {
            Assert.Equal(before, await Snapshot(data));
            entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return await Snapshot(data);
        }, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await store.WriteAsync(async data =>
            {
                await data.GetRepository<IMoveRepository>().SaveAsync(new CatalogMove(Guid.NewGuid(), "Concurrent with read", 40, PokemonType.Normal), default);
                return true;
            }, default);
        }
        finally { release.SetResult(); }
        Assert.Equal(before, await reading);
        Assert.Equal(22, await store.ReadAsync(async data => (await data.GetReader<IMoveReader>().ListAsync(new(), default)).Count, default));
    }

    /// <summary>Comprueba que los agregados almacenados inválidos se presentan como errores de infraestructura.</summary>
    [PostgresFact]
    public async Task Corrupt_stored_domain_state_is_an_infrastructure_error()
    {
        await using var db = await Database();
        await using var command = db.Source.CreateCommand("DELETE FROM pokedex_learned_moves WHERE pokemon_id=(SELECT id FROM pokedex_pokemon LIMIT 1)");
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source)).ReadAsync(Snapshot, default));
    }
    /// <summary>Comprueba que una lectura selectiva no materializa otros agregados corruptos ajenos a la consulta.</summary>
    [PostgresFact]
    public async Task Selective_reads_do_not_materialize_unrelated_corrupt_aggregates()
    {
        await using var db = await Database();
        var store = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        await using var corrupt = db.Source.CreateCommand("DELETE FROM pokedex_learned_moves WHERE pokemon_id='00000000-0000-0000-0000-000000000201'");
        await corrupt.ExecuteNonQueryAsync();
        var move = await store.ReadAsync(data => data.GetReader<IMoveReader>().FindAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"), default), default);
        Assert.NotNull(move);
        var unaffected = await store.ReadAsync(data => data.GetReader<IOwnedPokemonReader>().FindAsync(Guid.Parse("00000000-0000-0000-0000-000000000202"), default), default);
        Assert.NotNull(unaffected);
        Assert.Equal(4, unaffected.MoveIds.Count);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(data => data.GetReader<IOwnedPokemonReader>().FindAsync(Guid.Parse("00000000-0000-0000-0000-000000000201"), default), default));
    }

    /// <summary>Comprueba la paginación SQL estable y el tratamiento de los nombres como parámetros.</summary>
    [PostgresFact]
    public async Task Sql_pages_are_stable_and_names_are_parameters()
    {
        await using var db = await Database();
        var store = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        var first = await store.ReadAsync(data => data.GetReader<IMoveReader>().ListAsync(new(0, 2), default), default);
        var second = await store.ReadAsync(data => data.GetReader<IMoveReader>().ListAsync(new(2, 2), default), default);
        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Empty(first.Select(move => move.Id).Intersect(second.Select(move => move.Id)));
        var combined = await store.ReadAsync(data => data.GetReader<IMoveReader>().ListAsync(new(0, 4), default), default);
        Assert.Equal(first.Concat(second).Select(move => move.Id), combined.Select(move => move.Id));
        var quoted = new CatalogMove(Guid.NewGuid(), "O'Brien'; --", 40, PokemonType.Normal);
        await store.WriteAsync(async data => { await data.GetRepository<IMoveRepository>().SaveAsync(quoted, default); return true; }, default);
        Assert.True(await store.ReadAsync(data => data.GetReader<IMoveReader>().NameExistsAsync(quoted.Name, Guid.Empty, default), default));
        Assert.False(await store.ReadAsync(data => data.GetReader<IMoveReader>().NameExistsAsync(quoted.Name, quoted.Id, default), default));
        Assert.Equal(22, await store.ReadAsync(async data => (await data.GetReader<IMoveReader>().ListAsync(new(), default)).Count, default));
    }
}
