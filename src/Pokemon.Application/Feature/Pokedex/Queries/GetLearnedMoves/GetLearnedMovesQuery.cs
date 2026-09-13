using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetLearnedMoves;

/// <summary>Consulta GetLearnedMoves: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record GetLearnedMovesQuery(Guid Id) : IRequest<PokemonView>;

public sealed class GetLearnedMovesQueryHandler(IPokedexStore store) : IRequestHandler<GetLearnedMovesQuery, PokemonView>
{
    public Task<PokemonView> Handle(GetLearnedMovesQuery request, CancellationToken token) =>
        store.Read<PokemonView>(data => PokedexMapping.View(data, PokedexMapping.Pokemon(data, request.Id)), token);

}
