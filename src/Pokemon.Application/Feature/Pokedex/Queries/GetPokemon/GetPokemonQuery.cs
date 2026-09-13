using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetPokemon;

/// <summary>Consulta GetPokemon: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record GetPokemonQuery(Guid Id) : IRequest<PokemonView>;

public sealed class GetPokemonQueryHandler(IPokedexStore store) : IRequestHandler<GetPokemonQuery, PokemonView>
{
    public Task<PokemonView> Handle(GetPokemonQuery request, CancellationToken token) =>
        store.Read<PokemonView>(data => PokedexMapping.View(data, PokedexMapping.Pokemon(data, request.Id)), token);

}
