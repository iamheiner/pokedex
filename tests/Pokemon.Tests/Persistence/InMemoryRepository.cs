using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

internal abstract class InMemoryRepository<T>(Func<T, Guid> id, Action ensure) where T : class
{
    internal readonly Dictionary<Guid, T> Entries = [];
    protected Task<R> Result<R>(R result, CancellationToken token) { ensure(); token.ThrowIfCancellationRequested(); return Task.FromResult(result); }
    public Task<T?> FindAsync(Guid identity, CancellationToken token) => Result(Entries.GetValueOrDefault(identity), token);
    public Task SaveAsync(T value, CancellationToken token) { ensure(); token.ThrowIfCancellationRequested(); Entries[id(value)] = value; return Task.CompletedTask; }
    public Task DeleteAsync(Guid identity, CancellationToken token) { ensure(); token.ThrowIfCancellationRequested(); Entries.Remove(identity); return Task.CompletedTask; }
    protected static IReadOnlyList<T> Page(IEnumerable<T> values, CatalogPage page) => values.Skip(page.Offset).Take(page.Limit).ToArray();
}
