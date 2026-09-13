using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.CreateMove;

public sealed record CreateMoveCommand(MoveInput Data) : ICommand<MoveView>;

/// <summary>
/// Coordina reglas y persistencia del agregado en una única transacción.
/// </summary>
public sealed class CreateMoveCommandHandler(IUnitOfWork transactions) : IRequestHandler<CreateMoveCommand, MoveView>
{
    /// <summary>
    /// Valida, guarda y devuelve un nuevo movimiento dentro de una transacción.
    /// </summary>
    public Task<MoveView> Handle(CreateMoveCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            var entity = await PokedexChanges.MoveAsync(data, Guid.NewGuid(), request.Data, token);
            await data.GetRepository<IMoveRepository>().SaveAsync(entity, token);
            return PokedexMapping.View(entity);
        }, token);
}
