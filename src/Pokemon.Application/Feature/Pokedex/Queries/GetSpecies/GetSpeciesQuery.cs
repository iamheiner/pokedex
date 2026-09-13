using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetSpecies;

public sealed record GetSpeciesQuery(Guid Id) : IQuery<SpeciesView>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class GetSpeciesQueryHandler(IReadSession reader) : IRequestHandler<GetSpeciesQuery, SpeciesView>
{
    public Task<SpeciesView> Handle(GetSpeciesQuery request, CancellationToken token) =>
        reader.ReadAsync<SpeciesView>(async data =>
        {
            return await PokedexMapping.ViewAsync(data, await PokedexMapping.SpeciesAsync(data, request.Id, token), token);
        }, token);
}
