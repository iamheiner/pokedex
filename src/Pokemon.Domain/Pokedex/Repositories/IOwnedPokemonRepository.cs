using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>
/// Define las operaciones de escritura de ejemplares dentro de una unidad de trabajo.
/// </summary>
public interface IOwnedPokemonRepository : IWriteRepository
{
    /// <summary>
    /// Inserta o actualiza el ejemplar y conserva el orden de sus cuatro movimientos aprendidos.
    /// </summary>
    Task SaveAsync(OwnedPokemon aggregate, CancellationToken token);

    /// <summary>
    /// Elimina el ejemplar y las referencias a sus movimientos aprendidos dentro de la operación actual.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken token);
}
