using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.UpdateMove;

/// <summary>Actualización de Move: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record UpdateMoveCommand(Guid Id, MoveInput Data) : IRequest<MoveView>;

public sealed class UpdateMoveCommandHandler(IPokedexUnitOfWork store) : IRequestHandler<UpdateMoveCommand, MoveView>
{
    public Task<MoveView> Handle(UpdateMoveCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            PokedexMapping.Move(data, request.Id);
            var entity = PokedexChanges.Move(data, request.Id, request.Data);
            data.MoveRepository.Save(entity);
            return PokedexMapping.View(entity);
        }, token);
}
