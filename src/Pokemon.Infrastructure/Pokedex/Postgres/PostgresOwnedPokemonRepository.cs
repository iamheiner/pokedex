using Npgsql;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Pokedex.Postgres;

internal sealed class PostgresOwnedPokemonRepository : PostgresRepository<OwnedPokemon>, IOwnedPokemonRepository
{
    public PostgresOwnedPokemonRepository() : base(pokemon => pokemon.Id) { }
    public async Task Load(ISpeciesRepository species, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        var learned = new Dictionary<Guid, List<Guid>>();
        await using (var command = new NpgsqlCommand("SELECT pokemon_id,move_id FROM pokedex_learned_moves ORDER BY pokemon_id,slot", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token))
            {
                var id = reader.GetGuid(0);
                if (!learned.TryGetValue(id, out var list)) learned[id] = list = [];
                list.Add(reader.GetGuid(1));
            }
        await using var select = new NpgsqlCommand("SELECT id,species_id,name,level,current_health,total_health FROM pokedex_pokemon", connection, transaction);
        await using var rows = await select.ExecuteReaderAsync(token);
        while (await rows.ReadAsync(token))
        {
            var id = rows.GetGuid(0);
            var ownerSpecies = species.Find(rows.GetGuid(1)) ?? throw new InvalidDataException("Stored Pokemon species is missing.");
            Entries.Add(id, new OwnedPokemon(id,ownerSpecies,rows.GetString(2),rows.GetInt32(3),rows.GetInt32(4),rows.GetInt32(5),learned.GetValueOrDefault(id) ?? []));
        }
    }
    protected override Task DeleteRow(Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token) =>
        Execute("DELETE FROM pokedex_pokemon WHERE id=$1", connection, transaction, token, id);
    protected override async Task SaveRow(OwnedPokemon pokemon, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        await Execute("INSERT INTO pokedex_pokemon (id,species_id,name,level,current_health,total_health) VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT(id) DO UPDATE SET species_id=excluded.species_id,name=excluded.name,level=excluded.level,current_health=excluded.current_health,total_health=excluded.total_health",
            connection,transaction,token,pokemon.Id,pokemon.SpeciesId,pokemon.Name,pokemon.Level,pokemon.CurrentHealth,pokemon.TotalHealth);
        await Execute("DELETE FROM pokedex_learned_moves WHERE pokemon_id=$1",connection,transaction,token,pokemon.Id);
        for (var slot=0;slot<pokemon.MoveIds.Count;slot++)
            await Execute("INSERT INTO pokedex_learned_moves (pokemon_id,slot,move_id) VALUES ($1,$2,$3)",connection,transaction,token,pokemon.Id,slot,pokemon.MoveIds[slot]);
    }
}
