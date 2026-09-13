using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Persistencia del agregado OwnedPokemon dentro de una unidad de trabajo.</summary>
public interface IOwnedPokemonRepository : IWriteRepository
{
    Task SaveAsync(OwnedPokemon aggregate, CancellationToken token);
    Task DeleteAsync(Guid id, CancellationToken token);
}
