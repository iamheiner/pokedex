namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Contrato de lectura selectiva; no expone escritura, SQL ni tecnología de almacenamiento.</summary>
public interface ISpeciesReader
{
    Task<Species?> FindAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<Species>> ListAsync(CatalogPage page, CancellationToken token);
    Task<IReadOnlyList<Species>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task<IReadOnlyList<Species>> FindByMoveAsync(Guid moveId, CatalogPage page, CancellationToken token);
    Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token);
    Task<bool> ReferencesMoveAsync(Guid moveId, CancellationToken token);
}
