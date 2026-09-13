using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

internal sealed class InMemorySpeciesRepository(Action ensure) : InMemoryRepository<Species>(value => value.Id, ensure), ISpeciesRepository, ISpeciesReader
{
    public Task<IReadOnlyList<Species>> ListAsync(CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id), page), token);
    public Task<IReadOnlyList<Species>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Result<IReadOnlyList<Species>>(Entries.Values.Where(value => ids.Contains(value.Id)).ToArray(), token);
    public Task<IReadOnlyList<Species>> FindByMoveAsync(Guid id, CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.Where(value => value.Learnset.Any(entry => entry.MoveId == id)).OrderBy(value => value.Id), page), token);
    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token) => Result(Entries.Values.Any(value => value.Id != exceptId && string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)), token);
    public Task<bool> ReferencesMoveAsync(Guid id, CancellationToken token) => Result(Entries.Values.Any(value => value.Learnset.Any(entry => entry.MoveId == id)), token);
}
