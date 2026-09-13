using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Define las operaciones de escritura de movimientos dentro de una unidad de trabajo.</summary>
public interface IMoveRepository : IWriteRepository
{
    /// <summary>Inserta o actualiza los datos del movimiento dentro de la operación actual.</summary>
    Task SaveAsync(CatalogMove aggregate, CancellationToken token);

    /// <summary>Elimina el movimiento indicado dentro de la operación actual.</summary>
    Task DeleteAsync(Guid id, CancellationToken token);
}
