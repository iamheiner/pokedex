using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>
/// Define consultas de especies sin conceder operaciones de escritura.
/// </summary>
public interface ISpeciesReader : IReadRepository
{
    /// <summary>
    /// Busca una especie por su identificador y devuelve null si no existe.
    /// </summary>
    Task<Species?> FindAsync(Guid id, CancellationToken token);

    /// <summary>
    /// Devuelve una página de especies ordenada por nombre e identificador.
    /// </summary>
    Task<IReadOnlyList<Species>> ListAsync(CatalogPage page, CancellationToken token);

    /// <summary>
    /// Recupera las especies existentes que coinciden con los identificadores solicitados.
    /// </summary>
    Task<IReadOnlyList<Species>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);

    /// <summary>
    /// Devuelve una página de especies que pueden aprender el movimiento, ordenada por identificador.
    /// </summary>
    Task<IReadOnlyList<Species>> FindByMoveAsync(Guid moveId, CatalogPage page, CancellationToken token);

    /// <summary>
    /// Comprueba si otra especie utiliza el nombre indicado, sin distinguir mayúsculas.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token);

    /// <summary>
    /// Comprueba si algún plan de aprendizaje incluye el movimiento indicado.
    /// </summary>
    Task<bool> ReferencesMoveAsync(Guid moveId, CancellationToken token);
}
