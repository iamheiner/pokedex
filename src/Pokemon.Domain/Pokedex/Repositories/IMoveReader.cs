using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>
/// Define consultas de movimientos sin conceder operaciones de escritura.
/// </summary>
public interface IMoveReader : IReadRepository
{
    /// <summary>
    /// Busca un movimiento por su identificador y devuelve null si no existe.
    /// </summary>
    Task<CatalogMove?> FindAsync(Guid id, CancellationToken token);

    /// <summary>
    /// Devuelve una página de movimientos ordenada por nombre e identificador.
    /// </summary>
    Task<IReadOnlyList<CatalogMove>> ListAsync(CatalogPage page, CancellationToken token);

    /// <summary>
    /// Recupera los movimientos existentes que coinciden con los identificadores solicitados.
    /// </summary>
    Task<IReadOnlyList<CatalogMove>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);

    /// <summary>
    /// Comprueba si otro movimiento utiliza el nombre indicado, sin distinguir mayúsculas.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token);
}
