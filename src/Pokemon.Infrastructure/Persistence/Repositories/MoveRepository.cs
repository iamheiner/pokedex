using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Persistence.Repositories;

/// <summary>
/// Consulta y persiste movimientos mediante Dapper en la transacción de la unidad de trabajo.
/// </summary>
internal sealed class MoveRepository(CatalogDatabaseSession session) : IMoveRepository, IMoveReader
{
    private const string Columns = "id AS Id, name AS Name, power AS Power, type AS Type";

    /// <summary>
    /// Representa los datos de una fila de movimientos antes de reconstruir el agregado.
    /// </summary>
    private sealed record Row(Guid Id, string Name, int Power, int Type)
    {
        /// <summary>
        /// Reconstruye el movimiento y trata los datos almacenados inválidos como un error de persistencia.
        /// </summary>
        public CatalogMove ToDomain() => DomainMaterializer.Create(() => new CatalogMove(Id, Name, Power, (PokemonType)Type));
    }

    /// <summary>
    /// Ejecuta un filtro SQL interno parametrizado y reconstruye los movimientos encontrados.
    /// </summary>
    private async Task<IReadOnlyList<CatalogMove>> Select(string filter, object args, CancellationToken token) =>
        (await session.QueryAsync<Row>($"SELECT {Columns} FROM pokedex_moves {filter}", args, token)).Select(row => row.ToDomain()).ToArray();

    /// <summary>
    /// Busca un movimiento por su identificador y devuelve null si no existe.
    /// </summary>
    public async Task<CatalogMove?> FindAsync(Guid id, CancellationToken token) =>
        (await Select("WHERE id=@Id", new { Id = id }, token)).SingleOrDefault();

    /// <summary>
    /// Devuelve una página de movimientos ordenada por nombre e identificador.
    /// </summary>
    public Task<IReadOnlyList<CatalogMove>> ListAsync(CatalogPage page, CancellationToken token) =>
        Select("ORDER BY upper(name),id OFFSET @Offset LIMIT @Limit", page, token);

    /// <summary>
    /// Recupera los movimientos existentes que coinciden con los identificadores solicitados.
    /// </summary>
    public Task<IReadOnlyList<CatalogMove>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) =>
        Select("WHERE id=ANY(@Ids)", new { Ids = ids.ToArray() }, token);

    /// <summary>
    /// Comprueba si otro movimiento utiliza el nombre indicado, sin distinguir mayúsculas.
    /// </summary>
    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token) =>
        session.ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM pokedex_moves WHERE upper(name)=upper(@Name) AND id<>@ExceptId)", new { Name = name, ExceptId = exceptId }, token);

    /// <summary>
    /// Inserta o actualiza los datos del movimiento dentro de la operación actual.
    /// </summary>
    public Task SaveAsync(CatalogMove move, CancellationToken token) => session.ExecuteAsync("""
        INSERT INTO pokedex_moves(id,name,power,type) VALUES(@Id,@Name,@Power,@Type)
        ON CONFLICT(id) DO UPDATE SET name=excluded.name,power=excluded.power,type=excluded.type
        """, new { move.Id, move.Name, move.Power, Type = (int)move.Type }, token);

    /// <summary>
    /// Elimina el movimiento indicado dentro de la operación actual.
    /// </summary>
    public Task DeleteAsync(Guid id, CancellationToken token) =>
        session.ExecuteAsync("DELETE FROM pokedex_moves WHERE id=@Id", new { Id = id }, token);
}
