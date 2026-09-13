using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.UpdateMove;

public sealed record UpdateMoveCommand(Guid Id, MoveInput Data) : ICommand<MoveView>;

/// <summary>Coordina reglas y persistencia del agregado en una única transacción.</summary>
public sealed class UpdateMoveCommandHandler(IUnitOfWork transactions) : IRequestHandler<UpdateMoveCommand, MoveView>
{
    public Task<MoveView> Handle(UpdateMoveCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            await PokedexMapping.MoveAsync(data, request.Id, token);
            var entity = await PokedexChanges.MoveAsync(data, request.Id, request.Data, token);
            await data.GetRepository<IMoveRepository>().SaveAsync(entity, token);
            return PokedexMapping.View(entity);
        }, token);
}
