using Npgsql;
using NpgsqlTypes;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Battle;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Infrastructure.Battle.Postgres;

/// <summary>
/// Guarda un agregado por fila. SELECT FOR UPDATE serializa únicamente las acciones de
/// la misma partida, también entre procesos. El turno y su historial se confirman juntos.
/// </summary>
public sealed class PostgresBattleRepository(NpgsqlDataSource source) : IBattleRepository
{
    public async Task Add(BattleAggregate battle, CancellationToken token)
    {
        await using var command = source.CreateCommand("INSERT INTO battles (id, version, document) VALUES ($1, $2, $3)");
        command.Parameters.AddWithValue(battle.Id);
        command.Parameters.AddWithValue(battle.Version);
        command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, BattleDocumentCodec.Serialize(battle));
        try { await command.ExecuteNonQueryAsync(token); }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new BattleConflictException("Battle identity already exists."); }
    }

    public async Task<BattleAggregate> Get(Guid id, CancellationToken token)
    {
        await using var command = source.CreateCommand("SELECT version, document::text FROM battles WHERE id = $1");
        command.Parameters.AddWithValue(id);
        return await Read(command, id, token);
    }

    public async Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var select = new NpgsqlCommand("SELECT version, document::text FROM battles WHERE id = $1 FOR UPDATE", connection, transaction);
        select.Parameters.AddWithValue(id);
        var original = await Read(select, id, token);
        token.ThrowIfCancellationRequested();
        // El callback valida expectedVersion ANTES de obtener azar. Una petición que esperó
        // el bloqueo ve la versión confirmada por su predecesora y no ejecuta otro ataque.
        var updated = action(original);
        if (updated.Id != id || updated.Version != original.Version + 1)
            throw new InvalidOperationException("A turn must advance exactly one version of the same battle.");
        token.ThrowIfCancellationRequested();
        await using var update = new NpgsqlCommand("UPDATE battles SET version = $1, document = $2, updated_at = now() WHERE id = $3 AND version = $4", connection, transaction);
        update.Parameters.AddWithValue(updated.Version);
        update.Parameters.AddWithValue(NpgsqlDbType.Jsonb, BattleDocumentCodec.Serialize(updated));
        update.Parameters.AddWithValue(id);
        update.Parameters.AddWithValue(original.Version);
        if (await update.ExecuteNonQueryAsync(token) != 1)
            throw new BattleConflictException("Battle version changed during the update.");
        await transaction.CommitAsync(token);
        return updated;
    }

    private static async Task<BattleAggregate> Read(NpgsqlCommand command, Guid id, CancellationToken token)
    {
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new BattleNotFoundException("Battle was not found.");
        var battle = BattleDocumentCodec.Deserialize(reader.GetString(1));
        if (battle.Id != id || battle.Version != reader.GetInt32(0))
            throw new InvalidDataException("Stored battle identity or version does not match its document.");
        return battle;
    }
}
