using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetSpeciesSharingMove;

public sealed record GetSpeciesSharingMoveQuery(Guid Id, int Offset = 0, int Limit = 100) : IQuery<IReadOnlyList<SpeciesView>>;

/// <summary>Consulta con acceso exclusivamente de lectura; carga solo las identidades o página solicitadas.</summary>
public sealed class GetSpeciesSharingMoveQueryHandler(IReadSession reader) : IRequestHandler<GetSpeciesSharingMoveQuery, IReadOnlyList<SpeciesView>>
{
    public Task<IReadOnlyList<SpeciesView>> Handle(GetSpeciesSharingMoveQuery request, CancellationToken token) =>
        reader.ReadAsync<IReadOnlyList<SpeciesView>>(async data =>
        {
            await PokedexMapping.MoveAsync(data, request.Id, token);
            return await PokedexMapping.ViewsAsync(data, await data.GetReader<ISpeciesReader>().FindByMoveAsync(request.Id, new CatalogPage(request.Offset, request.Limit), token), token);
        }, token);
}
