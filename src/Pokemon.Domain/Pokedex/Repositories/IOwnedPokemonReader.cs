using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Define consultas de ejemplares sin conceder operaciones de escritura.</summary>
public interface IOwnedPokemonReader : IReadRepository
{
    /// <summary>Busca un ejemplar por su identificador y devuelve null si no existe.</summary>
    Task<OwnedPokemon?> FindAsync(Guid id, CancellationToken token);

    /// <summary>Devuelve una página de ejemplares ordenada por nombre e identificador.</summary>
    Task<IReadOnlyList<OwnedPokemon>> ListAsync(CatalogPage page, CancellationToken token);

    /// <summary>Devuelve una página de ejemplares que tienen aprendido el movimiento, ordenada por identificador.</summary>
    Task<IReadOnlyList<OwnedPokemon>> FindByMoveAsync(Guid moveId, CatalogPage page, CancellationToken token);

    /// <summary>Recupera todos los ejemplares de la especie para comprobar las reglas que los afectan.</summary>
    Task<IReadOnlyList<OwnedPokemon>> FindBySpeciesAsync(Guid speciesId, CancellationToken token);

    /// <summary>Comprueba si existe algún ejemplar de la especie indicada.</summary>
    Task<bool> ReferencesSpeciesAsync(Guid speciesId, CancellationToken token);

    /// <summary>Comprueba si algún ejemplar tiene aprendido el movimiento indicado.</summary>
    Task<bool> ReferencesMoveAsync(Guid moveId, CancellationToken token);
}
