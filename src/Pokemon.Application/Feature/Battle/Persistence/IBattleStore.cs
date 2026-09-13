using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Application.Feature.Battle.Persistence;

/// <summary>Puerto de partidas: la actualización de una acción debe ser atómica y conservar su versión.</summary>
public interface IBattleStore
{
    Task Add(BattleAggregate battle, CancellationToken token);
    Task<BattleAggregate> Get(Guid id, CancellationToken token);
    Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token);
}
