using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
namespace Pokemon.Infrastructure.Persistence;

internal sealed class PersistenceHealthCheck(DatabaseConnectionFactory connections) : IHealthCheck
{
    /// <summary>Comprueba la conexión y las versiones de los esquemas de partidas y Pokédex.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            await using var session = await connections.BeginAsync(IsolationLevel.ReadCommitted, token);
            var ready = await session.ScalarAsync<bool>("""
                SELECT EXISTS(SELECT 1 FROM battle_schema_migrations WHERE version=1)
                AND EXISTS(SELECT 1 FROM pokedex_schema_migrations WHERE version=1)
                """, null, token);
            await session.CommitAsync(token);
            return ready ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Persistence schemas are not ready.");
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException)
        { return HealthCheckResult.Unhealthy("Persistence is unavailable."); }
    }
}
