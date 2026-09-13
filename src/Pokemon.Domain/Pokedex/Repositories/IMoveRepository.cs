namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Persistencia del agregado CatalogMove dentro de una unidad de trabajo.</summary>
public interface IMoveRepository : IMoveReader
{
    Task SaveAsync(CatalogMove aggregate, CancellationToken token);
    Task DeleteAsync(Guid id, CancellationToken token);
}
