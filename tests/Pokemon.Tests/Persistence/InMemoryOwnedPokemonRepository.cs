using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Persistence;

internal sealed class InMemoryOwnedPokemonRepository(Action ensure) : InMemoryRepository<OwnedPokemon>(value => value.Id, ensure), IOwnedPokemonRepository
{
    public Task<IReadOnlyList<OwnedPokemon>> ListAsync(CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id), page), token);
    public Task<IReadOnlyList<OwnedPokemon>> FindByMoveAsync(Guid id, CatalogPage page, CancellationToken token) => Result(Page(Entries.Values.Where(value => value.MoveIds.Contains(id)).OrderBy(value => value.Id), page), token);
    public Task<IReadOnlyList<OwnedPokemon>> FindBySpeciesAsync(Guid id, CancellationToken token) => Result<IReadOnlyList<OwnedPokemon>>(Entries.Values.Where(value => value.SpeciesId == id).ToArray(), token);
    public Task<bool> ReferencesSpeciesAsync(Guid id, CancellationToken token) => Result(Entries.Values.Any(value => value.SpeciesId == id), token);
    public Task<bool> ReferencesMoveAsync(Guid id, CancellationToken token) => Result(Entries.Values.Any(value => value.MoveIds.Contains(id)), token);
}
