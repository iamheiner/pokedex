using Pokemon.Domain.Battle.Exceptions;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Application.Common.Messaging;
using MediatR;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Application.Feature.Battle.Commands.CreateBattle;

public sealed record CreateBattleCommand(CreateBattleInput Data) : ICommand<BattleView>;

/// <summary>
/// Crea una partida aislada a partir de dos ejemplares existentes, sin modificar la colección.
/// </summary>
public sealed class CreateBattleCommandHandler(IUnitOfWork transactions)
    : IRequestHandler<CreateBattleCommand, BattleView>
{
    /// <summary>
    /// Crea y guarda una partida a partir de dos ejemplares dentro de una única unidad de trabajo.
    /// </summary>
    public async Task<BattleView> Handle(CreateBattleCommand request, CancellationToken token)
    {
        var input = request.Data;
        if (input is null || input.FirstPokemonId == Guid.Empty || input.SecondPokemonId == Guid.Empty)
            throw new BattleRuleException("Two Pokemon identities are required.");
        return await transactions.WriteAsync(async data =>
        {
            var battle = BattleAggregate.Start(Guid.NewGuid(),
                await BattleMapping.SnapshotAsync(data, input.FirstPokemonId, token),
                await BattleMapping.SnapshotAsync(data, input.SecondPokemonId, token));
            await data.GetRepository<IBattleRepository>().Add(battle, token);
            return BattleMapping.View(battle);
        }, token);
    }
}
