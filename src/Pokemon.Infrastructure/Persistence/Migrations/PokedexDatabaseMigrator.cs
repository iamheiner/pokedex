using System.Data;
using Pokemon.Infrastructure.Pokedex;
namespace Pokemon.Infrastructure.Persistence.Migrations;

/// <summary>Migración versionada y atómica. Conserva el historial y nunca reinicializa datos existentes.</summary>
internal sealed class PokedexDatabaseMigrator(DatabaseConnectionFactory connections)
{
    public async Task Migrate(CancellationToken token)
    {
        await using var session = await connections.BeginAsync(IsolationLevel.ReadCommitted, token);
        await session.ExecuteAsync("SELECT pg_advisory_xact_lock(724619830126)", null, token);
        await session.ExecuteAsync("CREATE TABLE IF NOT EXISTS pokedex_schema_migrations(version integer PRIMARY KEY,applied_at timestamptz NOT NULL DEFAULT now())", null, token);
        var version = await session.ScalarAsync<int>("SELECT COALESCE(MAX(version),0) FROM pokedex_schema_migrations", null, token);
        if (version > 1) throw new InvalidOperationException("Pokedex schema is newer than this application.");
        if (version == 0)
        {
            await using var stream = typeof(PokedexDatabaseMigrator).Assembly.GetManifestResourceStream("PokedexMigration001.sql")
                ?? throw new InvalidOperationException("Missing database migration.");
            using var reader = new StreamReader(stream);
            await session.ExecuteAsync(await reader.ReadToEndAsync(token), null, token);
            await PokedexSeed.PopulateAsync(new RepositoryScope(session, RepositoryRegistry.CreateDefault(), readOnly: false), token);
            await session.ExecuteAsync("INSERT INTO pokedex_schema_migrations(version) VALUES(1)", null, token);
        }
        await session.CommitAsync(token);
    }
}
