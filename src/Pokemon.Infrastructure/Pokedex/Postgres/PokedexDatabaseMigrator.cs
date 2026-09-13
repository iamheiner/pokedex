using Npgsql;
using Pokemon.Domain.Pokedex;
namespace Pokemon.Infrastructure.Pokedex.Postgres;

/// <summary>Esquema y catálogo inicial en una sola transacción; nunca repuebla un catálogo ya inicializado.</summary>
public sealed class PokedexDatabaseMigrator(NpgsqlDataSource source)
{
    public async Task Migrate(CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await PostgresRepository<CatalogMove>.Execute("SELECT pg_advisory_xact_lock(724619830126)",connection,transaction,token);
        await PostgresRepository<CatalogMove>.Execute("CREATE TABLE IF NOT EXISTS pokedex_schema_migrations(version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())",connection,transaction,token);
        await using var current = new NpgsqlCommand("SELECT COALESCE(MAX(version),0) FROM pokedex_schema_migrations",connection,transaction);
        var version = Convert.ToInt32(await current.ExecuteScalarAsync(token));
        if (version > 1) throw new InvalidOperationException("Pokedex schema is newer than this application.");
        if (version == 0)
        {
            await using var stream = typeof(PokedexDatabaseMigrator).Assembly.GetManifestResourceStream("PokedexMigration001.sql")
                ?? throw new InvalidOperationException("Missing Pokedex migration.");
            using var reader = new StreamReader(stream);
            await PostgresRepository<CatalogMove>.Execute(await reader.ReadToEndAsync(token),connection,transaction,token);
            var session = new PostgresPokedexSession();
            PokedexSeed.Populate(session);
            await session.Flush(connection,transaction,token);
            await PostgresRepository<CatalogMove>.Execute("INSERT INTO pokedex_schema_migrations(version) VALUES (1)",connection,transaction,token);
        }
        await transaction.CommitAsync(token);
    }
}
