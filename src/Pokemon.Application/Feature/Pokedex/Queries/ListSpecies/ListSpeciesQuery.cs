using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.ListSpecies;

public sealed record ListSpeciesQuery(int Offset = 0, int Limit = 100) : IQuery<IReadOnlyList<SpeciesView>>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class ListSpeciesQueryHandler(IReadSession reader) : IRequestHandler<ListSpeciesQuery, IReadOnlyList<SpeciesView>>
{
    /// <summary>Devuelve la página solicitada de especies con sus planes de aprendizaje.</summary>
    public Task<IReadOnlyList<SpeciesView>> Handle(ListSpeciesQuery request, CancellationToken token) =>
        reader.ReadAsync<IReadOnlyList<SpeciesView>>(async data =>
        {
            return await PokedexMapping.ViewsAsync(data, await data.GetReader<ISpeciesReader>().ListAsync(new CatalogPage(request.Offset, request.Limit), token), token);
        }, token);
}
