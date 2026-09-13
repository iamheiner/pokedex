namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Repositorio de OwnedPokemon dentro de una unidad de trabajo; no confirma cambios por sí solo.</summary>
public interface IOwnedPokemonRepository
{
    IReadOnlyCollection<OwnedPokemon> List();
    OwnedPokemon? Find(Guid id);
    void Save(OwnedPokemon aggregate);
    void Delete(Guid id);
}
