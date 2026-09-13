using System.Data;
using Npgsql;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Garantiza coherencia de lecturas y atomicidad de comandos sin precargar el catálogo.</summary>
internal sealed class PokedexSessionFactory(DatabaseConnectionFactory connections) : IPokedexReadSession, IPokedexUnitOfWork
{
    public async Task<T> ReadAsync<T>(Func<IPokedexReader, Task<T>> query, CancellationToken token)
    {
        await using var session = await connections.BeginAsync(IsolationLevel.RepeatableRead, token);
        await session.ExecuteAsync("SET TRANSACTION READ ONLY", null, token);
        var result = await query(new PokedexSession(session));
        await session.CommitAsync(token);
        return result;
    }
    public async Task<T> WriteAsync<T>(Func<IPokedexSession, Task<T>> command, CancellationToken token)
    {
        await using var session = await connections.BeginAsync(IsolationLevel.ReadCommitted, token);
        // Las reglas entre agregados del catálogo se evalúan sobre el último commit.
        await session.ExecuteAsync("SELECT pg_advisory_xact_lock(724619830127)", null, token);
        try
        {
            var result = await command(new PokedexSession(session));
            await session.CommitAsync(token);
            return result;
        }
        catch (PostgresException error) when (error.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        { throw new PokedexConflictException("The change conflicts with an existing name or reference."); }
    }
}
