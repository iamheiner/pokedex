using MediatR;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Application.Feature.Battle.Commands.CreateBattle;

public sealed record CreateBattleCommand(CreateBattleInput Data) : IRequest<BattleView>;

/// <summary>Crea una partida aislada a partir de dos ejemplares existentes, sin modificar la colección.</summary>
public sealed class CreateBattleCommandHandler(IPokedexUnitOfWork pokedex, IBattleRepository battles)
    : IRequestHandler<CreateBattleCommand, BattleView>
{
    public async Task<BattleView> Handle(CreateBattleCommand request, CancellationToken token)
    {
        var input = request.Data;
        if (input is null || input.FirstPokemonId == Guid.Empty || input.SecondPokemonId == Guid.Empty)
            throw new BattleRuleException("Two Pokemon identities are required.");
        var battle = await pokedex.Read(data => BattleAggregate.Start(Guid.NewGuid(),
            BattleMapping.Snapshot(data, input.FirstPokemonId), BattleMapping.Snapshot(data, input.SecondPokemonId)), token);
        await battles.Add(battle, token);
        return BattleMapping.View(battle);
    }
}
