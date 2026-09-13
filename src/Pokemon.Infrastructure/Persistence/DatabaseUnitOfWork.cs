using System.Data;
using Npgsql;
using Pokemon.Domain.Common.Exceptions;
using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Controla una transacción por operación, independientemente de los agregados que participen.</summary>
internal sealed class DatabaseUnitOfWork(DatabaseConnectionFactory connections, RepositoryRegistry? registrations = null) : IUnitOfWork, IReadSession
{
    private readonly RepositoryRegistry registry = registrations ?? RepositoryRegistry.CreateDefault();
    /// <summary>Ejecuta una consulta en una transacción de solo lectura con aislamiento repetible.</summary>
    public async Task<T> ReadAsync<T>(Func<IReadRepositoryScope, Task<T>> query, CancellationToken token)
    {
        await using var session = await connections.BeginAsync(IsolationLevel.RepeatableRead, token);
        await session.ExecuteAsync("SET TRANSACTION READ ONLY", null, token);
        var result = await query(new RepositoryScope(session, registry, readOnly: true));
        await session.CommitAsync(token);
        return result;
    }
    /// <summary>Ejecuta y confirma un comando completo o revierte sus cambios si la operación falla.</summary>
    public async Task<T> WriteAsync<T>(Func<IRepositoryScope, Task<T>> command, CancellationToken token)
    {
        await using var session = await connections.BeginAsync(IsolationLevel.ReadCommitted, token);
        try
        {
            var result = await command(new RepositoryScope(session, registry, readOnly: false));
            await session.CommitAsync(token);
            return result;
        }
        catch (PostgresException error) when (error.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        { throw new PersistenceConflictException("The change conflicts with an existing name or reference."); }
    }
}
