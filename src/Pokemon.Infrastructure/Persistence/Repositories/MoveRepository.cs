using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Persistence.Repositories;

internal sealed class MoveRepository(CatalogDatabaseSession session) : IMoveRepository, IMoveReader
{
    private const string Columns = "id AS Id, name AS Name, power AS Power, type AS Type";
    private sealed record Row(Guid Id, string Name, int Power, int Type)
    {
        public CatalogMove ToDomain() => DomainMaterializer.Create(() => new CatalogMove(Id, Name, Power, (PokemonType)Type));
    }
    private async Task<IReadOnlyList<CatalogMove>> Select(string filter, object args, CancellationToken token) =>
        (await session.QueryAsync<Row>($"SELECT {Columns} FROM pokedex_moves {filter}", args, token)).Select(row => row.ToDomain()).ToArray();
    public async Task<CatalogMove?> FindAsync(Guid id, CancellationToken token) =>
        (await Select("WHERE id=@Id", new { Id = id }, token)).SingleOrDefault();
    public Task<IReadOnlyList<CatalogMove>> ListAsync(CatalogPage page, CancellationToken token) =>
        Select("ORDER BY upper(name),id OFFSET @Offset LIMIT @Limit", page, token);
    public Task<IReadOnlyList<CatalogMove>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) =>
        Select("WHERE id=ANY(@Ids)", new { Ids = ids.ToArray() }, token);
    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token) =>
        session.ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM pokedex_moves WHERE upper(name)=upper(@Name) AND id<>@ExceptId)", new { Name = name, ExceptId = exceptId }, token);
    public Task SaveAsync(CatalogMove move, CancellationToken token) => session.ExecuteAsync("""
        INSERT INTO pokedex_moves(id,name,power,type) VALUES(@Id,@Name,@Power,@Type)
        ON CONFLICT(id) DO UPDATE SET name=excluded.name,power=excluded.power,type=excluded.type
        """, new { move.Id, move.Name, move.Power, Type = (int)move.Type }, token);
    public Task DeleteAsync(Guid id, CancellationToken token) =>
        session.ExecuteAsync("DELETE FROM pokedex_moves WHERE id=@Id", new { Id = id }, token);
}
