using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreateMove;

/// <summary>Creación de Move: aplica reglas y guarda el cambio de forma atómica.</summary>
public sealed record CreateMoveCommand(MoveInput Data) : IRequest<MoveView>;

public sealed class CreateMoveCommandHandler(IPokedexUnitOfWork store) : IRequestHandler<CreateMoveCommand, MoveView>
{
    public Task<MoveView> Handle(CreateMoveCommand request, CancellationToken token) =>
        store.Write(data =>
        {
            var entity = PokedexChanges.Move(data, Guid.NewGuid(), request.Data);
            data.MoveRepository.Save(entity);
            return PokedexMapping.View(entity);
        }, token);
}
