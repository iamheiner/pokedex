using Npgsql;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Pokedex.Postgres;

internal sealed class PostgresMoveRepository : PostgresRepository<CatalogMove>, IMoveRepository
{
    public PostgresMoveRepository() : base(move => move.Id) { }
    public async Task Load(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("SELECT id, name, power, type FROM pokedex_moves", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var move = new CatalogMove(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), (PokemonType)reader.GetInt32(3));
            Entries.Add(move.Id, move);
        }
    }
    protected override Task DeleteRow(Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token) =>
        Execute("DELETE FROM pokedex_moves WHERE id = $1", connection, transaction, token, id);
    protected override Task SaveRow(CatalogMove move, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token) =>
        Execute("INSERT INTO pokedex_moves (id,name,power,type) VALUES ($1,$2,$3,$4) ON CONFLICT (id) DO UPDATE SET name=excluded.name,power=excluded.power,type=excluded.type",
            connection, transaction, token, move.Id, move.Name, move.Power, (int)move.Type);
}
