using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetPossibleMoves;

public sealed record GetPossibleMovesQuery(Guid Id) : IQuery<PossibleMovesView>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class GetPossibleMovesQueryHandler(IReadSession reader) : IRequestHandler<GetPossibleMovesQuery, PossibleMovesView>
{
    /// <summary>Devuelve el plan completo de movimientos posibles del ejemplar, incluidos los niveles futuros.</summary>
    public Task<PossibleMovesView> Handle(GetPossibleMovesQuery request, CancellationToken token) =>
        reader.ReadAsync<PossibleMovesView>(async data =>
        {
            var pokemon = await PokedexMapping.PokemonAsync(data, request.Id, token);
            var species = await PokedexMapping.SpeciesAsync(data, pokemon.SpeciesId, token);
            var view = await PokedexMapping.ViewAsync(data, species, token);
            return new PossibleMovesView(pokemon.Id, pokemon.SpeciesId, view.Learnset);
        }, token);
}
