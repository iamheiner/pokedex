using Pokemon.Domain.Battle.Exceptions;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Battle;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Tests.Persistence;

/// <summary>Simula un repositorio de partidas en memoria con actualizaciones atómicas exclusivamente para las pruebas.</summary>
public sealed class InMemoryBattleRepository : IBattleRepository, IBattleReader, IDisposable
{
    private readonly Dictionary<Guid, BattleAggregate> battles = [];
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Añade una partida completa y rechaza un identificador que ya existe.</summary>
    public async Task Add(BattleAggregate battle, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!battles.TryAdd(battle.Id, battle)) throw new BattleConflictException("Battle identity already exists.");
        }
        finally { gate.Release(); }
    }

    /// <summary>Recupera una partida por su identificador o informa de que no existe.</summary>
    public async Task<BattleAggregate> Get(Guid id, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { token.ThrowIfCancellationRequested(); return Find(id); }
        finally { gate.Release(); }
    }

    /// <summary>Aplica una acción de forma atómica y exige avanzar una única versión de la misma partida.</summary>
    public async Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var original = Find(id);
            var updated = action(original);
            if (updated.Id != id || updated.Version != original.Version + 1)
                throw new InvalidOperationException("A turn must advance exactly one version of the same battle.");
            token.ThrowIfCancellationRequested();
            battles[id] = updated;
            return updated;
        }
        finally { gate.Release(); }
    }

    /// <summary>Busca la partida en el estado de prueba o informa de que no existe.</summary>
    private BattleAggregate Find(Guid id) => battles.TryGetValue(id, out var battle) ? battle
        : throw new BattleNotFoundException("Battle was not found.");

    /// <summary>Libera el semáforo utilizado para coordinar el acceso al repositorio de prueba.</summary>
    public void Dispose() => gate.Dispose();
}
