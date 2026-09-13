using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

/// <summary>
/// Simula las consultas de ejemplares en memoria exclusivamente para las pruebas.
/// </summary>
internal sealed class InMemoryOwnedPokemonRepository(Action ensure) : InMemoryRepository<OwnedPokemon>(value => value.Id, ensure), IOwnedPokemonRepository, IOwnedPokemonReader
{
    /// <summary>
    /// Devuelve una página de ejemplares ordenada por nombre e identificador.
    /// </summary>
    public Task<IReadOnlyList<OwnedPokemon>> ListAsync(CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id), page), token);

    /// <summary>
    /// Devuelve una página de ejemplares que tienen aprendido el movimiento, ordenada por identificador.
    /// </summary>
    public Task<IReadOnlyList<OwnedPokemon>> FindByMoveAsync(Guid id, CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.Where(value => value.MoveIds.Contains(id)).OrderBy(value => value.Id), page), token);

    /// <summary>
    /// Recupera todos los ejemplares de la especie para comprobar las reglas que los afectan.
    /// </summary>
    public Task<IReadOnlyList<OwnedPokemon>> FindBySpeciesAsync(Guid id, CancellationToken token) => Result<IReadOnlyList<OwnedPokemon>>(Entries.Values.Where(value => value.SpeciesId == id).ToArray(), token);

    /// <summary>
    /// Comprueba si existe algún ejemplar de la especie indicada.
    /// </summary>
    public Task<bool> ReferencesSpeciesAsync(Guid id, CancellationToken token) => Result(Entries.Values.Any(value => value.SpeciesId == id), token);

    /// <summary>
    /// Comprueba si algún ejemplar tiene aprendido el movimiento indicado.
    /// </summary>
    public Task<bool> ReferencesMoveAsync(Guid id, CancellationToken token) => Result(Entries.Values.Any(value => value.MoveIds.Contains(id)), token);
}
