using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>
/// Define las operaciones de escritura de especies dentro de una unidad de trabajo.
/// </summary>
public interface ISpeciesRepository : IWriteRepository
{
    /// <summary>
    /// Inserta o actualiza la especie y reemplaza su plan de aprendizaje dentro de la operación actual.
    /// </summary>
    Task SaveAsync(Species aggregate, CancellationToken token);

    /// <summary>
    /// Elimina la especie y su plan de aprendizaje dentro de la operación actual.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken token);
}
