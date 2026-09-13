using Pokemon.Domain.Common.Persistence;
using Pokemon.Infrastructure.Persistence;
using Pokemon.Infrastructure.Persistence.Repositories;
using Pokemon.Infrastructure.Persistence.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pokemon.Domain.Pokedex.Repositories;
using Npgsql;
using Pokemon.Domain.Battle.Repositories;
namespace Pokemon.Infrastructure;

/// <summary>
/// Composición del adaptador. PostgreSQL es el único almacenamiento de la aplicación.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra PostgreSQL, la unidad de trabajo común, las fábricas de repositorios y las migraciones.
    /// </summary>
    public static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks();
        var connection = configuration.GetConnectionString("Battles");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:Battles is required for PostgreSQL. Use Docker Compose or configure the connection explicitly.");
        services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
        services.AddSingleton<DatabaseConnectionFactory>();
        services.AddSingleton(RepositoryRegistry.CreateDefault());
        services.AddSingleton<DatabaseUnitOfWork>();
        services.AddSingleton<IUnitOfWork>(provider => provider.GetRequiredService<DatabaseUnitOfWork>());
        services.AddSingleton<IReadSession>(provider => provider.GetRequiredService<DatabaseUnitOfWork>());
        services.AddSingleton<PokedexDatabaseMigrator>();
        services.AddSingleton<BattleDatabaseMigrator>();
        services.AddHostedService<PersistenceMigrationService>();
        services.AddHealthChecks().AddCheck<PersistenceHealthCheck>("persistence");
    }
}

/// <summary>
/// No acepta peticiones hasta que el esquema esté listo; un fallo de base de datos no activa memoria silenciosamente.
/// </summary>
internal sealed class PersistenceMigrationService(BattleDatabaseMigrator migrator, PokedexDatabaseMigrator pokedex) : IHostedService
{
    /// <summary>
    /// Aplica las migraciones de partidas y Pokédex antes de que el servicio acepte peticiones.
    /// </summary>
    public async Task StartAsync(CancellationToken token)
    {
        await migrator.Migrate(token);
        await pokedex.Migrate(token);
    }
    /// <summary>
    /// Completa la parada del servicio de migraciones, que no mantiene tareas en segundo plano.
    /// </summary>
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}
