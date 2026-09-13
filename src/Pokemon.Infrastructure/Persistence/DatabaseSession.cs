using System.Data;
using Dapper;
using Npgsql;
namespace Pokemon.Infrastructure.Persistence;

/// <summary>
/// Detalle interno de infraestructura: Dapper siempre recibe la transacción y la cancelación.
/// </summary>
internal sealed class DatabaseSession(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
{
    private bool completed;
    /// <summary>
    /// Rechaza el uso de una sesión cuya transacción ya ha terminado.
    /// </summary>
    internal void EnsureActive() => ObjectDisposedException.ThrowIf(completed, this);
    /// <summary>
    /// Ejecuta una consulta parametrizada con Dapper dentro de la transacción y devuelve sus filas.
    /// </summary>
    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken token)
    {
        EnsureActive();
        return connection.QueryAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: token));
    }
    /// <summary>
    /// Ejecuta una consulta parametrizada y obtiene su primer valor dentro de la transacción.
    /// </summary>
    public Task<T> ScalarAsync<T>(string sql, object? parameters, CancellationToken token)
    {
        EnsureActive();
        return connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: token))!;
    }
    /// <summary>
    /// Ejecuta una instrucción parametrizada y devuelve el número de filas afectadas.
    /// </summary>
    public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken token)
    {
        EnsureActive();
        return connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: token));
    }
    /// <summary>
    /// Confirma la transacción y marca la sesión como terminada.
    /// </summary>
    public async Task CommitAsync(CancellationToken token)
    {
        EnsureActive();
        await transaction.CommitAsync(token);
        completed = true;
    }
    /// <summary>
    /// Cierra la transacción y libera la conexión, revirtiendo los cambios que no se hayan confirmado.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        completed = true;
        try { await transaction.DisposeAsync(); }
        finally { await connection.DisposeAsync(); }
    }
}

/// <summary>
/// El proveedor PostgreSQL se elige en composición; los contratos de dominio no lo conocen.
/// </summary>
internal sealed class DatabaseConnectionFactory(NpgsqlDataSource source)
{
    /// <summary>
    /// Abre una conexión y una transacción con el aislamiento solicitado, liberando la conexión si falla.
    /// </summary>
    public async Task<DatabaseSession> BeginAsync(IsolationLevel isolation, CancellationToken token)
    {
        var connection = await source.OpenConnectionAsync(token);
        try { return new(connection, await connection.BeginTransactionAsync(isolation, token)); }
        catch { await connection.DisposeAsync(); throw; }
    }
}
