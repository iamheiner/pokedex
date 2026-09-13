using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Commands.DeleteMove;

/// <summary>Eliminación de Move: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record DeleteMoveCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteMoveCommandHandler(IPokedexStore store) : IRequestHandler<DeleteMoveCommand, Unit>
{
    public Task<Unit> Handle(DeleteMoveCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            PokedexMapping.Move(data, request.Id);
            if (data.Species.Any(s => s.Learnset.Any(e => e.MoveId == request.Id)))
                throw new PokedexConflictException("Move is referenced by a species learnset.");
            data.DeleteMove(request.Id);
            return Unit.Value;
        }, token);
}
