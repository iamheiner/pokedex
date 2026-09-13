using System.Data;
using Npgsql;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Pokedex.Postgres;

/// <summary>
/// PostgreSQL es la fuente de verdad. Cada operación carga un snapshot transaccional.
/// Las escrituras del catálogo se serializan entre instancias para que las comprobaciones
/// de unicidad y referencias se apliquen sobre los últimos cambios confirmados.
/// </summary>
public sealed class PostgresPokedexUnitOfWork(NpgsqlDataSource source) : IPokedexUnitOfWork
{
    public async Task<T> Read<T>(Func<IPokedexReader, T> query, CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await PostgresRepository<CatalogMove>.Execute("SET TRANSACTION READ ONLY", connection,transaction,token);
        var session = new PostgresPokedexSession();
        await session.Load(connection,transaction,token);
        token.ThrowIfCancellationRequested();
        var result = query(session);
        await transaction.CommitAsync(token);
        return result;
    }
    public async Task<T> Write<T>(Func<IPokedexSession,T> command, CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await PostgresRepository<CatalogMove>.Execute("SELECT pg_advisory_xact_lock(724619830127)",connection,transaction,token);
        var session = new PostgresPokedexSession();
        await session.Load(connection,transaction,token);
        token.ThrowIfCancellationRequested();
        var result = command(session);
        token.ThrowIfCancellationRequested();
        try
        {
            await session.Flush(connection,transaction,token);
            await transaction.CommitAsync(token);
        }
        catch (PostgresException error) when (error.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new PokedexConflictException("The change conflicts with an existing name or reference.");
        }
        return result;
    }
}
