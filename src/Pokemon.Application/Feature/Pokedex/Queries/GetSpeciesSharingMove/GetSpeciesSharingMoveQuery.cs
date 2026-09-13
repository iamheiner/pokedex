using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetSpeciesSharingMove;

/// <summary>Consulta GetSpeciesSharingMove: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record GetSpeciesSharingMoveQuery(Guid Id) : IRequest<IReadOnlyList<SpeciesView>>;

public sealed class GetSpeciesSharingMoveQueryHandler(IPokedexUnitOfWork store) : IRequestHandler<GetSpeciesSharingMoveQuery, IReadOnlyList<SpeciesView>>
{
    public Task<IReadOnlyList<SpeciesView>> Handle(GetSpeciesSharingMoveQuery request, CancellationToken token) =>
        store.Read<IReadOnlyList<SpeciesView>>(data => SharingSpecies(data, request.Id), token);
    private static IReadOnlyList<SpeciesView> SharingSpecies(IPokedexReader data, Guid id)
    {
        PokedexMapping.Move(data, id);
        return data.Species.Where(s => s.Learnset.Any(e => e.MoveId == id)).OrderBy(s => s.Id).Select(s => PokedexMapping.View(data, s)).ToArray();
    }
}
