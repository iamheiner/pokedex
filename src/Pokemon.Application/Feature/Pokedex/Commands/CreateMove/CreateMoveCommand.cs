using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Persistence;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreateMove;

/// <summary>Creación de Move: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record CreateMoveCommand(MoveInput Data) : IRequest<MoveView>;

public sealed class CreateMoveCommandHandler(IPokedexStore store) : IRequestHandler<CreateMoveCommand, MoveView>
{
    public Task<MoveView> Handle(CreateMoveCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            var entity = PokedexChanges.Move(data, Guid.NewGuid(), request.Data);
            data.Save(entity);
            return PokedexMapping.View(entity);
        }, token);
}
