using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Domain.Battle.Repositories;

/// <summary>Repositorio del agregado Battle. Las implementaciones deben actualizar el agregado completo
/// atómicamente: una acción fallida no publica cambios y una versión obsoleta no consume azar.</summary>
public interface IBattleRepository : IBattleReader
{
    Task Add(BattleAggregate battle, CancellationToken token);
    Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token);
}
