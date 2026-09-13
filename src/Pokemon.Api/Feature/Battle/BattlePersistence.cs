using Npgsql;
using Pokemon.Application.Feature.Battle.Persistence;
using Pokemon.Infrastructure.Battle;
using Pokemon.Infrastructure.Battle.Postgres;
namespace Pokemon.Api.Feature.Battle;

/// <summary>Composición del adaptador. PostgreSQL es el modo normal; memoria requiere selección explícita.</summary>
public static class BattlePersistence
{
    public static void AddBattlePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks();
        var provider = configuration["BattlePersistence:Provider"] ?? "Postgres";
        if (string.Equals(provider, "Memory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IBattleStore, InMemoryBattleStore>();
            return;
        }
        if (!string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported BattlePersistence provider.");
        var connection = configuration.GetConnectionString("Battles");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:Battles is required for PostgreSQL. Use Docker Compose or configure the connection explicitly.");
        services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
        services.AddSingleton<IBattleStore, PostgresBattleStore>();
        services.AddSingleton<BattleDatabaseMigrator>();
        services.AddHostedService<BattleMigrationService>();
        services.AddHealthChecks().AddCheck<BattleDatabaseHealthCheck>("postgres-battles");
    }
}

/// <summary>No acepta peticiones hasta que el esquema esté listo; un fallo de base de datos no activa memoria silenciosamente.</summary>
internal sealed class BattleMigrationService(BattleDatabaseMigrator migrator) : IHostedService
{
    public Task StartAsync(CancellationToken token) => migrator.Migrate(token);
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}
