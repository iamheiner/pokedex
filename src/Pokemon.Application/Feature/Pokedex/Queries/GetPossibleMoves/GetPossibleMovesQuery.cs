using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetPossibleMoves;

/// <summary>Consulta GetPossibleMoves: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record GetPossibleMovesQuery(Guid Id) : IRequest<PossibleMovesView>;

public sealed class GetPossibleMovesQueryHandler(IPokedexUnitOfWork store) : IRequestHandler<GetPossibleMovesQuery, PossibleMovesView>
{
    public Task<PossibleMovesView> Handle(GetPossibleMovesQuery request, CancellationToken token) =>
        store.Read<PossibleMovesView>(data => Possible(data, request.Id), token);
    private static PossibleMovesView Possible(IPokedexReader data, Guid id)
    {
        var pokemon = PokedexMapping.Pokemon(data, id);
        return new(pokemon.Id, pokemon.SpeciesId, PokedexMapping.Learnset(data, PokedexMapping.Species(data, pokemon.SpeciesId)));
    }
}
