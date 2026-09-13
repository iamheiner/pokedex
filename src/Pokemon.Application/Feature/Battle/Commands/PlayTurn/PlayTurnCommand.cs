using MediatR;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Application.Feature.Damage;
namespace Pokemon.Application.Feature.Battle.Commands.PlayTurn;

public sealed record PlayTurnCommand(Guid BattleId, PlayTurnInput Data) : IRequest<BattleView>;

/// <summary>
/// Resuelve una sola acción dentro de la transacción. Valida la versión antes de obtener
/// un único factor aleatorio; los reintentos obsoletos no consumen usos ni producen otro golpe.
/// </summary>
public sealed class PlayTurnCommandHandler(IBattleRepository battles, IDamageRandom random)
    : IRequestHandler<PlayTurnCommand, BattleView>
{
    public async Task<BattleView> Handle(PlayTurnCommand request, CancellationToken token)
    {
        var input = request.Data;
        if (input is null || input.ExpectedVersion < 1)
            throw new BattleRuleException("Turn data and a positive expectedVersion are required.");
        var updated = await battles.Update(request.BattleId, battle =>
        {
            battle.EnsureCanAct(input.PokemonId, input.MoveId, input.ExpectedVersion);
            var factor = input.MoveId is null ? 100 : random.Next();
            return battle.PlayTurn(input.PokemonId, input.MoveId, input.ExpectedVersion, factor);
        }, token);
        return BattleMapping.View(updated);
    }
}
