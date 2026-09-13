using Npgsql;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Pokedex.Postgres;

internal sealed class PostgresSpeciesRepository : PostgresRepository<Species>, ISpeciesRepository
{
    public PostgresSpeciesRepository() : base(species => species.Id) { }
    public async Task Load(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        var learnsets = new Dictionary<Guid, List<LearnableMove>>();
        await using (var command = new NpgsqlCommand("SELECT species_id, move_id, level FROM pokedex_learnset ORDER BY species_id, level, move_id", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token))
            {
                var id = reader.GetGuid(0);
                if (!learnsets.TryGetValue(id, out var list)) learnsets[id] = list = [];
                list.Add(new LearnableMove(reader.GetGuid(1), reader.GetInt32(2)));
            }
        await using var select = new NpgsqlCommand("SELECT id,name,type,health,attack,defense,special_attack,special_defense,speed FROM pokedex_species", connection, transaction);
        await using var rows = await select.ExecuteReaderAsync(token);
        while (await rows.ReadAsync(token))
        {
            var id = rows.GetGuid(0);
            Entries.Add(id, new Species(id, rows.GetString(1), (PokemonType)rows.GetInt32(2),
                new BaseStats(rows.GetInt32(3),rows.GetInt32(4),rows.GetInt32(5),rows.GetInt32(6),rows.GetInt32(7),rows.GetInt32(8)),
                learnsets.GetValueOrDefault(id) ?? []));
        }
    }
    protected override Task DeleteRow(Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token) =>
        Execute("DELETE FROM pokedex_species WHERE id=$1", connection, transaction, token, id);
    protected override async Task SaveRow(Species species, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        var stats = species.Stats;
        await Execute("INSERT INTO pokedex_species (id,name,type,health,attack,defense,special_attack,special_defense,speed) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT (id) DO UPDATE SET name=excluded.name,type=excluded.type,health=excluded.health,attack=excluded.attack,defense=excluded.defense,special_attack=excluded.special_attack,special_defense=excluded.special_defense,speed=excluded.speed",
            connection, transaction, token, species.Id,species.Name,(int)species.Type,stats.Health,stats.Attack,stats.Defense,stats.SpecialAttack,stats.SpecialDefense,stats.Speed);
        await Execute("DELETE FROM pokedex_learnset WHERE species_id=$1", connection, transaction, token, species.Id);
        foreach (var entry in species.Learnset)
            await Execute("INSERT INTO pokedex_learnset (species_id,move_id,level) VALUES ($1,$2,$3)", connection, transaction, token, species.Id,entry.MoveId,entry.Level);
    }
}
