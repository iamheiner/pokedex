namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>Repositorios coordinados dentro de la misma transacción de escritura.</summary>
public interface IPokedexSession : IPokedexReader
{
    ISpeciesRepository SpeciesRepository { get; }
    IMoveRepository MoveRepository { get; }
    IOwnedPokemonRepository PokemonRepository { get; }
}
