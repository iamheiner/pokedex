using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

/// <summary>Comparte las operaciones básicas sobre el estado en memoria de los repositorios de prueba.</summary>
internal abstract class InMemoryRepository<T>(Func<T, Guid> id, Action ensure) where T : class
{
    internal readonly Dictionary<Guid, T> Entries = [];

    /// <summary>Devuelve el resultado después de comprobar que la sesión está activa y la operación no está cancelada.</summary>
    protected Task<R> Result<R>(R result, CancellationToken token) { ensure(); token.ThrowIfCancellationRequested(); return Task.FromResult(result); }

    /// <summary>Devuelve el resultado después de comprobar que la sesión está activa y la operación no está cancelada.</summary>
    public Task<T?> FindAsync(Guid identity, CancellationToken token) => Result(Entries.GetValueOrDefault(identity), token);

    /// <summary>Inserta o reemplaza un agregado en el estado de la operación de prueba.</summary>
    public Task SaveAsync(T value, CancellationToken token) { ensure(); token.ThrowIfCancellationRequested(); Entries[id(value)] = value; return Task.CompletedTask; }

    /// <summary>Elimina el agregado indicado del estado de la operación de prueba.</summary>
    public Task DeleteAsync(Guid identity, CancellationToken token) { ensure(); token.ThrowIfCancellationRequested(); Entries.Remove(identity); return Task.CompletedTask; }

    /// <summary>Selecciona el tramo solicitado de una secuencia previamente ordenada.</summary>
    protected static IReadOnlyList<T> Page(IEnumerable<T> values, CatalogPage page) => values.Skip(page.Offset).Take(page.Limit).ToArray();
}
