using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Persistence.Repositories;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Compone repositorios sobre la misma transacción; construir la sesión no carga datos.</summary>
internal sealed class PokedexSession : IPokedexSession
{
    public PokedexSession(DatabaseSession session)
    {
        MoveRepository = new MoveRepository(session);
        SpeciesRepository = new SpeciesRepository(session);
        PokemonRepository = new OwnedPokemonRepository(session, SpeciesRepository);
    }
    public ISpeciesRepository SpeciesRepository { get; }
    public IMoveRepository MoveRepository { get; }
    public IOwnedPokemonRepository PokemonRepository { get; }
    public ISpeciesReader Species => SpeciesRepository;
    public IMoveReader Moves => MoveRepository;
    public IOwnedPokemonReader Pokemon => PokemonRepository;
}
