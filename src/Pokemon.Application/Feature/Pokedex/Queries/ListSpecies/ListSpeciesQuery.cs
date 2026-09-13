using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Queries.ListSpecies;

/// <summary>Consulta ListSpecies: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record ListSpeciesQuery() : IRequest<IReadOnlyList<SpeciesView>>;

public sealed class ListSpeciesQueryHandler(IPokedexStore store) : IRequestHandler<ListSpeciesQuery, IReadOnlyList<SpeciesView>>
{
    public Task<IReadOnlyList<SpeciesView>> Handle(ListSpeciesQuery request, CancellationToken token) =>
        store.Read<IReadOnlyList<SpeciesView>>(data => data.Species.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id).Select(e => PokedexMapping.View(data, e)).ToArray(), token);

}
