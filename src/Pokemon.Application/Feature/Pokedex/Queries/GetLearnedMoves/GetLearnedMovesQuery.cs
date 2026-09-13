using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetLearnedMoves;

public sealed record GetLearnedMovesQuery(Guid Id) : IQuery<PokemonView>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class GetLearnedMovesQueryHandler(IReadSession reader) : IRequestHandler<GetLearnedMovesQuery, PokemonView>
{
    public Task<PokemonView> Handle(GetLearnedMovesQuery request, CancellationToken token) =>
        reader.ReadAsync<PokemonView>(async data =>
        {
            return await PokedexMapping.ViewAsync(data, await PokedexMapping.PokemonAsync(data, request.Id, token), token);
        }, token);
}
