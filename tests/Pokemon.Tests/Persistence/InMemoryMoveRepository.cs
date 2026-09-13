using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

internal sealed class InMemoryMoveRepository(Action ensure) : InMemoryRepository<CatalogMove>(value => value.Id, ensure), IMoveRepository, IMoveReader
{
    public Task<IReadOnlyList<CatalogMove>> ListAsync(CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id), page), token);
    public Task<IReadOnlyList<CatalogMove>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Result<IReadOnlyList<CatalogMove>>(Entries.Values.Where(value => ids.Contains(value.Id)).ToArray(), token);
    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token) => Result(Entries.Values.Any(value => value.Id != exceptId && string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)), token);
}
