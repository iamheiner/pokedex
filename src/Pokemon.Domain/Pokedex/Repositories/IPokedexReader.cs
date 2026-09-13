namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Acceso exclusivamente de lectura a los agregados que necesita una operación.</summary>
public interface IPokedexReader
{
    ISpeciesReader Species { get; }
    IMoveReader Moves { get; }
    IOwnedPokemonReader Pokemon { get; }
}
