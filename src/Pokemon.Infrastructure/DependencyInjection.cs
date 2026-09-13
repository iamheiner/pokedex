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

/// <summary>Composición del adaptador. PostgreSQL es el único almacenamiento de la aplicación.</summary>
public static class DependencyInjection
{
    public static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks();
        var connection = configuration.GetConnectionString("Battles");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:Battles is required for PostgreSQL. Use Docker Compose or configure the connection explicitly.");
        services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
        services.AddSingleton<DatabaseConnectionFactory>();
        services.AddSingleton<IBattleRepository, BattleRepository>();
        services.AddSingleton<IBattleReader>(provider => provider.GetRequiredService<IBattleRepository>());
        services.AddSingleton<PokedexSessionFactory>();
        services.AddSingleton<IPokedexUnitOfWork>(provider => provider.GetRequiredService<PokedexSessionFactory>());
        services.AddSingleton<IPokedexReadSession>(provider => provider.GetRequiredService<PokedexSessionFactory>());
        services.AddSingleton<PokedexDatabaseMigrator>();
        services.AddSingleton<BattleDatabaseMigrator>();
        services.AddHostedService<PersistenceMigrationService>();
        services.AddHealthChecks().AddCheck<PersistenceHealthCheck>("persistence");
    }
}

/// <summary>No acepta peticiones hasta que el esquema esté listo; un fallo de base de datos no activa memoria silenciosamente.</summary>
internal sealed class PersistenceMigrationService(BattleDatabaseMigrator migrator, PokedexDatabaseMigrator pokedex) : IHostedService
{
    public async Task StartAsync(CancellationToken token)
    {
        await migrator.Migrate(token);
        await pokedex.Migrate(token);
    }
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}
