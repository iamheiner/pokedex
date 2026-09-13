using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetSpecies;

/// <summary>Consulta GetSpecies: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record GetSpeciesQuery(Guid Id) : IRequest<SpeciesView>;

public sealed class GetSpeciesQueryHandler(IPokedexUnitOfWork store) : IRequestHandler<GetSpeciesQuery, SpeciesView>
{
    public Task<SpeciesView> Handle(GetSpeciesQuery request, CancellationToken token) =>
        store.Read<SpeciesView>(data => PokedexMapping.View(data, PokedexMapping.Species(data, request.Id)), token);

}
