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
    private static async Task<TestDatabase> Database()
    {
        var db = await TestDatabase.Create();
        try { await new PokedexDatabaseMigrator(new DatabaseConnectionFactory(db.Source)).Migrate(default); return db; }
        catch { await db.DisposeAsync(); throw; }
    }
    private static async Task<string> Snapshot(IPokedexReader data) => JsonSerializer.Serialize(new
    {
        Moves = (await data.Moves.ListAsync(new(), default)).OrderBy(value => value.Id),
        Species = (await data.Species.ListAsync(new(), default)).OrderBy(value => value.Id),
        Pokemon = (await data.Pokemon.ListAsync(new(), default)).OrderBy(value => value.Id)
    });

    [PostgresFact]
    public async Task Seed_runs_once_and_never_overwrites_changes_or_resurrects_deletions()
    {
        await using var db = await Database();
        var store = new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source));
        await store.WriteAsync(async data =>
        {
            var move = (await data.Moves.ListAsync(new(), default)).First();
            await data.MoveRepository.SaveAsync(new CatalogMove(move.Id, "Edited after seed", move.Power, move.Type), default);
            await data.PokemonRepository.DeleteAsync((await data.Pokemon.ListAsync(new(), default)).First().Id, default);
            return true;
        }, default);
        var before = await store.ReadAsync(Snapshot, default);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => new PokedexDatabaseMigrator(new DatabaseConnectionFactory(db.Source)).Migrate(default)));
        Assert.Equal(before, await store.ReadAsync(Snapshot, default));
        Assert.Equal(4, await store.ReadAsync(async data => (await data.Pokemon.ListAsync(new(), default)).Count, default));
    }

    [PostgresFact]
    public async Task Aggregates_round_trip_update_and_delete_across_new_connections()
    {
        await using var db = await Database();
        var store = new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source));
        var moves = Enumerable.Range(1, 4).Select(index => new CatalogMove(Guid.NewGuid(), $"Persisted {index}", index * 20, PokemonType.Fairy)).ToArray();
        var species = new Species(Guid.NewGuid(), "Persisted species", PokemonType.Fairy, Stats,
            moves.Select(move => new LearnableMove(move.Id, 1)));
        var pokemon = new OwnedPokemon(Guid.NewGuid(), species, "Persisted Pokemon", 20, 0, 40, moves.Reverse().Select(move => move.Id));
        await store.WriteAsync(async data =>
        {
            foreach (var move in moves) await data.MoveRepository.SaveAsync(move, default);
            await data.SpeciesRepository.SaveAsync(species, default);
            await data.PokemonRepository.SaveAsync(pokemon, default);
            return true;
        }, default);
        var before = await store.ReadAsync(Snapshot, default);
        await using var reconnected = NpgsqlDataSource.Create(db.ConnectionString);
        var restored = new PokedexSessionFactory(new DatabaseConnectionFactory(reconnected));
        Assert.Equal(before, await restored.ReadAsync(Snapshot, default));
        await restored.WriteAsync(async data =>
        {
            var saved = (await data.PokemonRepository.FindAsync(pokemon.Id, default))!;
            Assert.Equal(pokemon.MoveIds, saved.MoveIds);
            await data.PokemonRepository.SaveAsync(new OwnedPokemon(saved.Id, species, "Updated", saved.Level, 12, 40, saved.MoveIds), default);
            return true;
        }, default);
        Assert.Equal(12, await store.ReadAsync(async data => (await data.Pokemon.FindAsync(pokemon.Id, default))!.CurrentHealth, default));
        await restored.WriteAsync(async data =>
        {
            await data.PokemonRepository.DeleteAsync(pokemon.Id, default);
            await data.SpeciesRepository.DeleteAsync(species.Id, default);
            foreach (var move in moves) await data.MoveRepository.DeleteAsync(move.Id, default);
            return true;
        }, default);
        Assert.Equal((21, 5, 5), await store.ReadAsync(async data => ((await data.Moves.ListAsync(new(), default)).Count, (await data.Species.ListAsync(new(), default)).Count, (await data.Pokemon.ListAsync(new(), default)).Count), default));
    }

    [PostgresFact]
    public async Task Callback_failure_cancellation_and_database_failure_roll_back_all_changes()
    {
        await using var db = await Database();
        var store = new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source));
        var before = await store.ReadAsync(Snapshot, default);
        var move = new CatalogMove(Guid.NewGuid(), "Rollback", 40, PokemonType.Normal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync<int>(async data =>
        { await data.MoveRepository.SaveAsync(move, default); throw new InvalidOperationException(); }, default));
        using var cancelled = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(async data =>
        { await data.MoveRepository.SaveAsync(move, default); cancelled.Cancel(); return true; }, cancelled.Token));
        // The move INSERT succeeds, but the deferred foreign key fails at commit.
        await Assert.ThrowsAsync<PokedexConflictException>(() => store.WriteAsync(async data =>
        {
            await data.MoveRepository.SaveAsync(move, default);
            await data.SpeciesRepository.SaveAsync(new Species(Guid.NewGuid(), "Invalid foreign key", PokemonType.Normal, Stats,
                [new LearnableMove(Guid.NewGuid(), 1)]), default);
            return true;
        }, default));
        Assert.Equal(before, await store.ReadAsync(Snapshot, default));
    }

    [PostgresFact]
    public async Task Concurrent_instances_observe_latest_names_before_writing()
    {
        await using var db = await Database();
        await using var secondSource = NpgsqlDataSource.Create(db.ConnectionString);
        var repositories = new[] { new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source)), new PokedexSessionFactory(new DatabaseConnectionFactory(secondSource)) };
        var successes = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => repositories[index % 2].WriteAsync(async data =>
        {
            if (await data.Moves.NameExistsAsync("Concurrent", Guid.Empty, default)) return false;
            await data.MoveRepository.SaveAsync(new CatalogMove(Guid.NewGuid(), "Concurrent", 40, PokemonType.Normal), default);
            return true;
        }, default)));
        Assert.Single(successes, success => success);
        Assert.Single(await repositories[0].ReadAsync(async data => (await data.Moves.ListAsync(new(), default)).Where(move => move.Name == "Concurrent").ToArray(), default));
    }

    [PostgresFact]
    public async Task Escaped_session_cannot_write_after_transaction_completion()
    {
        await using var db = await Database();
        var store = new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source));
        IPokedexSession? escaped = null;
        await store.WriteAsync(data => { escaped = data; return Task.FromResult(true); }, default);
        var before = await store.ReadAsync(Snapshot, default);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => escaped!.MoveRepository.DeleteAsync(Guid.NewGuid(), default));
        var error = await Assert.ThrowsAsync<PostgresException>(() => store.ReadAsync(async data =>
        {
            await ((IPokedexSession)data).PokemonRepository.DeleteAsync(Guid.NewGuid(), default);
            return true;
        }, default));
        Assert.Equal(PostgresErrorCodes.ReadOnlySqlTransaction, error.SqlState);
        Assert.Equal(before, await store.ReadAsync(Snapshot, default));
    }

    [PostgresFact]
    public async Task Read_snapshot_does_not_mix_data_from_an_intervening_commit()
    {
        await using var db = await Database();
        var store = new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source));
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
                await data.MoveRepository.SaveAsync(new CatalogMove(Guid.NewGuid(), "Concurrent with read", 40, PokemonType.Normal), default);
                return true;
            }, default);
        }
        finally { release.SetResult(); }
        Assert.Equal(before, await reading);
        Assert.Equal(22, await store.ReadAsync(async data => (await data.Moves.ListAsync(new(), default)).Count, default));
    }

    [PostgresFact]
    public async Task Corrupt_stored_domain_state_is_an_infrastructure_error()
    {
        await using var db = await Database();
        await using var command = db.Source.CreateCommand("DELETE FROM pokedex_learned_moves WHERE pokemon_id=(SELECT id FROM pokedex_pokemon LIMIT 1)");
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source)).ReadAsync(Snapshot, default));
    }
    [PostgresFact]
    public async Task Selective_reads_do_not_materialize_unrelated_corrupt_aggregates()
    {
        await using var db = await Database();
        var store = new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source));
        await using var corrupt = db.Source.CreateCommand("DELETE FROM pokedex_learned_moves WHERE pokemon_id='00000000-0000-0000-0000-000000000201'");
        await corrupt.ExecuteNonQueryAsync();
        var move = await store.ReadAsync(data => data.Moves.FindAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"), default), default);
        Assert.NotNull(move);
        var unaffected = await store.ReadAsync(data => data.Pokemon.FindAsync(Guid.Parse("00000000-0000-0000-0000-000000000202"), default), default);
        Assert.NotNull(unaffected);
        Assert.Equal(4, unaffected.MoveIds.Count);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(data => data.Pokemon.FindAsync(Guid.Parse("00000000-0000-0000-0000-000000000201"), default), default));
    }

    [PostgresFact]
    public async Task Sql_pages_are_stable_and_names_are_parameters()
    {
        await using var db = await Database();
        var store = new PokedexSessionFactory(new DatabaseConnectionFactory(db.Source));
        var first = await store.ReadAsync(data => data.Moves.ListAsync(new(0, 2), default), default);
        var second = await store.ReadAsync(data => data.Moves.ListAsync(new(2, 2), default), default);
        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Empty(first.Select(move => move.Id).Intersect(second.Select(move => move.Id)));
        var combined = await store.ReadAsync(data => data.Moves.ListAsync(new(0, 4), default), default);
        Assert.Equal(first.Concat(second).Select(move => move.Id), combined.Select(move => move.Id));
        var quoted = new CatalogMove(Guid.NewGuid(), "O'Brien'; --", 40, PokemonType.Normal);
        await store.WriteAsync(async data => { await data.MoveRepository.SaveAsync(quoted, default); return true; }, default);
        Assert.True(await store.ReadAsync(data => data.Moves.NameExistsAsync(quoted.Name, Guid.Empty, default), default));
        Assert.False(await store.ReadAsync(data => data.Moves.NameExistsAsync(quoted.Name, quoted.Id, default), default));
        Assert.Equal(22, await store.ReadAsync(async data => (await data.Moves.ListAsync(new(), default)).Count, default));
    }
}
