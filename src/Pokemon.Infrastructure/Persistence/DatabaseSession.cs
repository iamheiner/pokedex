using System.Data;
using Dapper;
using Npgsql;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>Detalle interno de infraestructura: Dapper siempre recibe la transacción y la cancelación.</summary>
internal sealed class DatabaseSession(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
{
    private bool completed;
    private void EnsureActive() => ObjectDisposedException.ThrowIf(completed, this);
    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken token)
    {
        EnsureActive();
        return connection.QueryAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: token));
    }
    public Task<T> ScalarAsync<T>(string sql, object? parameters, CancellationToken token)
    {
        EnsureActive();
        return connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: token))!;
    }
    public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken token)
    {
        EnsureActive();
        return connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: token));
    }
    public async Task CommitAsync(CancellationToken token)
    {
        EnsureActive();
        await transaction.CommitAsync(token);
        completed = true;
    }
    public async ValueTask DisposeAsync()
    {
        completed = true;
        try { await transaction.DisposeAsync(); }
        finally { await connection.DisposeAsync(); }
    }
}

/// <summary>El proveedor PostgreSQL se elige en composición; los contratos de dominio no lo conocen.</summary>
internal sealed class DatabaseConnectionFactory(NpgsqlDataSource source)
{
    public async Task<DatabaseSession> BeginAsync(IsolationLevel isolation, CancellationToken token)
    {
        var connection = await source.OpenConnectionAsync(token);
        try { return new(connection, await connection.BeginTransactionAsync(isolation, token)); }
        catch { await connection.DisposeAsync(); throw; }
    }
}
