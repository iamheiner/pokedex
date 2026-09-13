using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Contrato de lectura selectiva; no expone escritura, SQL ni tecnología de almacenamiento.</summary>
public interface IMoveReader : IReadRepository
{
    Task<CatalogMove?> FindAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<CatalogMove>> ListAsync(CatalogPage page, CancellationToken token);
    Task<IReadOnlyList<CatalogMove>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token);
}
