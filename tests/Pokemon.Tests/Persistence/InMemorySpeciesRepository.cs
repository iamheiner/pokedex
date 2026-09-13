using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

/// <summary>Simula las consultas de especies en memoria exclusivamente para las pruebas.</summary>
internal sealed class InMemorySpeciesRepository(Action ensure) : InMemoryRepository<Species>(value => value.Id, ensure), ISpeciesRepository, ISpeciesReader
{
    /// <summary>Devuelve una página de especies ordenada por nombre e identificador.</summary>
    public Task<IReadOnlyList<Species>> ListAsync(CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id), page), token);

    /// <summary>Recupera las especies existentes que coinciden con los identificadores solicitados.</summary>
    public Task<IReadOnlyList<Species>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Result<IReadOnlyList<Species>>(Entries.Values.Where(value => ids.Contains(value.Id)).ToArray(), token);

    /// <summary>Devuelve una página de especies que pueden aprender el movimiento, ordenada por identificador.</summary>
    public Task<IReadOnlyList<Species>> FindByMoveAsync(Guid id, CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.Where(value => value.Learnset.Any(entry => entry.MoveId == id)).OrderBy(value => value.Id), page), token);

    /// <summary>Comprueba si otra especie utiliza el nombre indicado, sin distinguir mayúsculas.</summary>
    public Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken token) => Result(Entries.Values.Any(value => value.Id != exceptId && string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)), token);

    /// <summary>Comprueba si algún plan de aprendizaje incluye el movimiento indicado.</summary>
    public Task<bool> ReferencesMoveAsync(Guid id, CancellationToken token) => Result(Entries.Values.Any(value => value.Learnset.Any(entry => entry.MoveId == id)), token);
}
