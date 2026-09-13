using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

/// <summary>Simula las consultas de movimientos en memoria exclusivamente para las pruebas.</summary>
internal sealed class InMemoryMoveRepository(Action ensure) : InMemoryRepository<CatalogMove>(value => value.Id, ensure), IMoveRepository, IMoveReader
{
    /// <summary>Devuelve una página de movimientos ordenada por nombre e identificador.</summary>
    public Task<IReadOnlyList<CatalogMove>> ListAsync(CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id), page), token);

    /// <summary>Recupera los movimientos existentes que coinciden con los identificadores solicitados.</summary>
    public Task<IReadOnlyList<CatalogMove>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Result<IReadOnlyList<CatalogMove>>(Entries.Values.Where(value => ids.Contains(value.Id)).ToArray(), token);

    /// <summary>Comprueba si otro movimiento utiliza el nombre indicado, sin distinguir mayúsculas.</summary>
    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token) => Result(Entries.Values.Any(value => value.Id != exceptId && string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)), token);
}
