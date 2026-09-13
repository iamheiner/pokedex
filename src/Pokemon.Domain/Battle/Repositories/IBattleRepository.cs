using Pokemon.Domain.Common.Persistence;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Domain.Battle.Repositories;

/// <summary>
/// Define la escritura atómica del agregado de partida dentro de una unidad de trabajo.
/// </summary>
public interface IBattleRepository : IWriteRepository
{
    /// <summary>
    /// Añade una partida completa y rechaza un identificador que ya existe.
    /// </summary>
    Task Add(BattleAggregate battle, CancellationToken token);

    /// <summary>
    /// Aplica una acción de forma atómica y exige avanzar una única versión de la misma partida.
    /// </summary>
    Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token);
}
