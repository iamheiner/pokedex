using Pokemon.Domain.Battle.Exceptions;
using System.Data;
using Npgsql;
using Pokemon.Domain.Battle;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Infrastructure.Persistence.Serialization;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Infrastructure.Persistence.Repositories;

/// <summary>Persiste la raíz completa; el bloqueo por fila impide resolver dos turnos sobre la misma versión.</summary>
internal sealed class BattleRepository(DatabaseSession session) : IBattleRepository, IBattleReader
{
    private sealed record Row(int Version, string Document);
    public async Task Add(BattleAggregate battle, CancellationToken token)
    {
        try
        {
            await session.ExecuteAsync("INSERT INTO battles(id,version,document) VALUES(@Id,@Version,CAST(@Document AS jsonb))",
                new { battle.Id, battle.Version, Document = BattleDocumentCodec.Serialize(battle) }, token);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new BattleConflictException("Battle identity already exists."); }
    }
    public async Task<BattleAggregate> Get(Guid id, CancellationToken token)
    {
        var battle = await Read(session, id, false, token);
        return battle;
    }
    public async Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token)
    {
        var original = await Read(session, id, true, token);
        token.ThrowIfCancellationRequested();
        var updated = action(original);
        if (updated.Id != id || updated.Version != original.Version + 1)
            throw new InvalidOperationException("A turn must advance exactly one version of the same battle.");
        token.ThrowIfCancellationRequested();
        var affected = await session.ExecuteAsync("""
            UPDATE battles SET version=@Version,document=CAST(@Document AS jsonb),updated_at=now()
            WHERE id=@Id AND version=@OriginalVersion
            """, new { Id = id, updated.Version, Document = BattleDocumentCodec.Serialize(updated), OriginalVersion = original.Version }, token);
        if (affected != 1) throw new BattleConflictException("Battle version changed during the update.");
        return updated;
    }
    private static async Task<BattleAggregate> Read(DatabaseSession session, Guid id, bool forUpdate, CancellationToken token)
    {
        var row = (await session.QueryAsync<Row>("SELECT version AS Version,document::text AS Document FROM battles WHERE id=@Id" + (forUpdate ? " FOR UPDATE" : ""),
            new { Id = id }, token)).SingleOrDefault() ?? throw new BattleNotFoundException("Battle was not found.");
        var battle = BattleDocumentCodec.Deserialize(row.Document);
        if (battle.Id != id || battle.Version != row.Version)
            throw new InvalidDataException("Stored battle identity or version does not match its document.");
        return battle;
    }
}
