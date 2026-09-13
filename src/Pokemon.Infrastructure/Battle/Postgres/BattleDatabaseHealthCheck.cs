using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
namespace Pokemon.Infrastructure.Battle.Postgres;

/// <summary>Disponibilidad real del almacén y del esquema para la sonda de readiness.</summary>
internal sealed class BattleDatabaseHealthCheck(NpgsqlDataSource source) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            await using var command = source.CreateCommand("SELECT EXISTS (SELECT 1 FROM battle_schema_migrations WHERE version = 1) AND EXISTS (SELECT 1 FROM pokedex_schema_migrations WHERE version = 1)");
            return await command.ExecuteScalarAsync(token) is true ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Battle schema is not ready.");
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException)
        { return HealthCheckResult.Unhealthy("Battle database is unavailable."); }
    }
}
