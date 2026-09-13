using Pokemon.Domain.Battle.Exceptions;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Battle;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Tests.Persistence;

/// <summary>
/// Almacén de partidas de un proceso. El agregado es inmutable: solo se publica una nueva
/// referencia si la acción termina correctamente. Las partidas se pierden al reiniciar.
/// </summary>
public sealed class InMemoryBattleRepository : IBattleRepository, IBattleReader, IDisposable
{
    private readonly Dictionary<Guid, BattleAggregate> battles = [];
    private readonly SemaphoreSlim gate = new(1, 1);
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
    public async Task<BattleAggregate> Get(Guid id, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { token.ThrowIfCancellationRequested(); return Find(id); }
        finally { gate.Release(); }
    }
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
    private BattleAggregate Find(Guid id) => battles.TryGetValue(id, out var battle) ? battle
        : throw new BattleNotFoundException("Battle was not found.");
    public void Dispose() => gate.Dispose();
}
