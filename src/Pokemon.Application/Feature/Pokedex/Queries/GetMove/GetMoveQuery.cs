using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Queries.GetMove;

/// <summary>Consulta GetMove: proyecta una lectura coherente sin modificar el estado.</summary>
public sealed record GetMoveQuery(Guid Id) : IRequest<MoveView>;

public sealed class GetMoveQueryHandler(IPokedexStore store) : IRequestHandler<GetMoveQuery, MoveView>
{
    public Task<MoveView> Handle(GetMoveQuery request, CancellationToken token) =>
        store.Read<MoveView>(data => PokedexMapping.View(PokedexMapping.Move(data, request.Id)), token);

}
