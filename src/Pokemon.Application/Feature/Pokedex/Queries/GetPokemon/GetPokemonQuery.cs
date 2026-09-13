using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetPokemon;

public sealed record GetPokemonQuery(Guid Id) : IQuery<PokemonView>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class GetPokemonQueryHandler(IPokedexReadSession reader) : IRequestHandler<GetPokemonQuery, PokemonView>
{
    public Task<PokemonView> Handle(GetPokemonQuery request, CancellationToken token) =>
        reader.ReadAsync<PokemonView>(async data =>
        {
            return await PokedexMapping.ViewAsync(data, await PokedexMapping.PokemonAsync(data, request.Id, token), token);
        }, token);
}
