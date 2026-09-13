using System.Text.Json;
using Npgsql;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Pokedex.Postgres;
using Pokemon.Tests.Battle;

namespace Pokemon.Tests.Pokedex;

[Trait("Category", "Postgres")]
public sealed class PostgresPokedexTests
{
    private static readonly BaseStats Stats = new(40, 50, 60, 70, 80, 90);
    private static async Task<TestDatabase> Database()
    {
        var db = await TestDatabase.Create();
        try { await new PokedexDatabaseMigrator(db.Source).Migrate(default); return db; }
        catch { await db.DisposeAsync(); throw; }
    }
    private static string Snapshot(IPokedexReader data) => JsonSerializer.Serialize(new
    {
        Moves = data.Moves.OrderBy(value => value.Id),
        Species = data.Species.OrderBy(value => value.Id),
        Pokemon = data.Pokemon.OrderBy(value => value.Id)
    });

    [PostgresFact]
    public async Task Seed_runs_once_and_never_overwrites_changes_or_resurrects_deletions()
    {
        await using var db = await Database();
        var store = new PostgresPokedexUnitOfWork(db.Source);
        await store.Write(data =>
        {
            var move = data.Moves.First();
            data.MoveRepository.Save(new CatalogMove(move.Id, "Edited after seed", move.Power, move.Type));
            data.PokemonRepository.Delete(data.Pokemon.First().Id);
            return true;
        }, default);
        var before = await store.Read(Snapshot, default);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => new PokedexDatabaseMigrator(db.Source).Migrate(default)));
        Assert.Equal(before, await store.Read(Snapshot, default));
        Assert.Equal(4, await store.Read(data => data.Pokemon.Count, default));
    }

    [PostgresFact]
    public async Task Aggregates_round_trip_update_and_delete_across_new_connections()
    {
        await using var db = await Database();
        var store = new PostgresPokedexUnitOfWork(db.Source);
        var moves = Enumerable.Range(1,4).Select(index => new CatalogMove(Guid.NewGuid(), $"Persisted {index}", index*20, PokemonType.Fairy)).ToArray();
        var species = new Species(Guid.NewGuid(), "Persisted species", PokemonType.Fairy, Stats,
            moves.Select(move => new LearnableMove(move.Id, 1)));
        var pokemon = new OwnedPokemon(Guid.NewGuid(), species, "Persisted Pokemon", 20, 0, 40, moves.Reverse().Select(move => move.Id));
        await store.Write(data =>
        {
            foreach (var move in moves) data.MoveRepository.Save(move);
            data.SpeciesRepository.Save(species);
            data.PokemonRepository.Save(pokemon);
            return true;
        }, default);
        var before = await store.Read(Snapshot, default);
        await using var reconnected = NpgsqlDataSource.Create(db.ConnectionString);
        var restored = new PostgresPokedexUnitOfWork(reconnected);
        Assert.Equal(before, await restored.Read(Snapshot, default));
        await restored.Write(data =>
        {
            var saved = data.PokemonRepository.Find(pokemon.Id)!;
            Assert.Equal(pokemon.MoveIds, saved.MoveIds);
            data.PokemonRepository.Save(new OwnedPokemon(saved.Id, species, "Updated", saved.Level, 12, 40, saved.MoveIds));
            return true;
        }, default);
        Assert.Equal(12, await store.Read(data => data.Pokemon.Single(value => value.Id == pokemon.Id).CurrentHealth, default));
        await restored.Write(data =>
        {
            data.PokemonRepository.Delete(pokemon.Id);
            data.SpeciesRepository.Delete(species.Id);
            foreach (var move in moves) data.MoveRepository.Delete(move.Id);
            return true;
        }, default);
        Assert.Equal((21,5,5), await store.Read(data => (data.Moves.Count,data.Species.Count,data.Pokemon.Count), default));
    }

    [PostgresFact]
    public async Task Callback_failure_cancellation_and_database_failure_roll_back_all_changes()
    {
        await using var db = await Database();
        var store = new PostgresPokedexUnitOfWork(db.Source);
        var before = await store.Read(Snapshot, default);
        var move = new CatalogMove(Guid.NewGuid(), "Rollback", 40, PokemonType.Normal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Write<int>(data =>
        { data.MoveRepository.Save(move); throw new InvalidOperationException(); }, default));
        using var cancelled = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Write(data =>
        { data.MoveRepository.Save(move); cancelled.Cancel(); return true; }, cancelled.Token));
        // The move INSERT succeeds, but the deferred foreign key fails at commit.
        await Assert.ThrowsAsync<PokedexConflictException>(() => store.Write(data =>
        {
            data.MoveRepository.Save(move);
            data.SpeciesRepository.Save(new Species(Guid.NewGuid(), "Invalid foreign key", PokemonType.Normal, Stats,
                [new LearnableMove(Guid.NewGuid(), 1)]));
            return true;
        }, default));
        Assert.Equal(before, await store.Read(Snapshot, default));
    }

    [PostgresFact]
    public async Task Concurrent_instances_observe_latest_names_before_writing()
    {
        await using var db = await Database();
        await using var secondSource = NpgsqlDataSource.Create(db.ConnectionString);
        var repositories = new[] { new PostgresPokedexUnitOfWork(db.Source), new PostgresPokedexUnitOfWork(secondSource) };
        var successes = await Task.WhenAll(Enumerable.Range(0,12).Select(index => repositories[index % 2].Write(data =>
        {
            if (data.Moves.Any(move => move.Name == "Concurrent")) return false;
            data.MoveRepository.Save(new CatalogMove(Guid.NewGuid(), "Concurrent", 40, PokemonType.Normal));
            return true;
        }, default)));
        Assert.Single(successes, success => success);
        Assert.Single(await repositories[0].Read(data => data.Moves.Where(move => move.Name == "Concurrent").ToArray(), default));
    }

    [PostgresFact]
    public async Task Escaped_session_cannot_write_after_transaction_completion()
    {
        await using var db = await Database();
        var store = new PostgresPokedexUnitOfWork(db.Source);
        IPokedexSession? escaped = null;
        await store.Write(data => { escaped = data; return true; }, default);
        var before = await store.Read(Snapshot, default);
        escaped!.MoveRepository.Delete(escaped.Moves.First().Id);
        await store.Read(data => { ((IPokedexSession)data).PokemonRepository.Delete(data.Pokemon.First().Id); return true; }, default);
        Assert.Equal(before, await store.Read(Snapshot, default));
    }

    [PostgresFact]
    public async Task Read_snapshot_does_not_mix_data_from_an_intervening_commit()
    {
        await using var db = await Database();
        var store = new PostgresPokedexUnitOfWork(db.Source);
        var before = await store.Read(Snapshot, default);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var reading = Task.Run(() => store.Read(data =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return Snapshot(data);
        }, default));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            await store.Write(data =>
            {
                data.MoveRepository.Save(new CatalogMove(Guid.NewGuid(), "Concurrent with read", 40, PokemonType.Normal));
                return true;
            }, default);
        }
        finally { release.Set(); }
        Assert.Equal(before, await reading);
        Assert.Equal(22, await store.Read(data => data.Moves.Count, default));
    }

    [PostgresFact]
    public async Task Corrupt_stored_domain_state_is_an_infrastructure_error()
    {
        await using var db = await Database();
        await using var command = db.Source.CreateCommand("DELETE FROM pokedex_learned_moves WHERE pokemon_id=(SELECT id FROM pokedex_pokemon LIMIT 1)");
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => new PostgresPokedexUnitOfWork(db.Source).Read(Snapshot, default));
    }
}
