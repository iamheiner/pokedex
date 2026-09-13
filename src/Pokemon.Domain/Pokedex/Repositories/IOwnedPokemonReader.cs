namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Contrato de lectura selectiva; no expone escritura, SQL ni tecnología de almacenamiento.</summary>
public interface IOwnedPokemonReader
{
    Task<OwnedPokemon?> FindAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<OwnedPokemon>> ListAsync(CatalogPage page, CancellationToken token);
    Task<IReadOnlyList<OwnedPokemon>> FindByMoveAsync(Guid moveId, CatalogPage page, CancellationToken token);
    Task<IReadOnlyList<OwnedPokemon>> FindBySpeciesAsync(Guid speciesId, CancellationToken token);
    Task<bool> ReferencesSpeciesAsync(Guid speciesId, CancellationToken token);
    Task<bool> ReferencesMoveAsync(Guid moveId, CancellationToken token);
}
