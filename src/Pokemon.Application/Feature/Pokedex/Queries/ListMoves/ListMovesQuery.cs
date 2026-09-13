using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Queries.ListMoves;

/// <summary>Consulta ListMoves: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record ListMovesQuery() : IRequest<IReadOnlyList<MoveView>>;

public sealed class ListMovesQueryHandler(IPokedexStore store) : IRequestHandler<ListMovesQuery, IReadOnlyList<MoveView>>
{
    public Task<IReadOnlyList<MoveView>> Handle(ListMovesQuery request, CancellationToken token) =>
        store.Read<IReadOnlyList<MoveView>>(data => data.Moves.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id).Select(e => PokedexMapping.View(e)).ToArray(), token);

}
