using Pokemon.Domain.Pokedex.Exceptions;
using Pokemon.Domain.Common.Persistence;
using MediatR;
using Pokemon.Application.Common.Messaging;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Application.Feature.Pokedex.Commands.DeleteMove;

public sealed record DeleteMoveCommand(Guid Id) : ICommand<Unit>;

/// <summary>Coordina reglas y persistencia del agregado en una única transacción.</summary>
public sealed class DeleteMoveCommandHandler(IUnitOfWork transactions) : IRequestHandler<DeleteMoveCommand, Unit>
{
    public Task<Unit> Handle(DeleteMoveCommand request, CancellationToken token) =>
        transactions.WriteAsync(async data =>
        {
            await PokedexMapping.MoveAsync(data, request.Id, token);
            if (await data.GetReader<ISpeciesReader>().ReferencesMoveAsync(request.Id, token) || await data.GetReader<IOwnedPokemonReader>().ReferencesMoveAsync(request.Id, token))
                throw new PokedexConflictException("Move is referenced by a species or Pokemon.");
            await data.GetRepository<IMoveRepository>().DeleteAsync(request.Id, token);
            return Unit.Value;
        }, token);
}
