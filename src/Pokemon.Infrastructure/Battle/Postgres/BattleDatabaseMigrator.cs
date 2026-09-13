using Npgsql;
namespace Pokemon.Infrastructure.Battle.Postgres;

/// <summary>Migraciones SQL versionadas y atómicas; el bloqueo transaccional coordina arranques simultáneos.</summary>
public sealed class BattleDatabaseMigrator(NpgsqlDataSource source)
{
    public async Task Migrate(CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        // Clave exclusiva de las migraciones de esta aplicación; se libera al terminar la transacción.
        await Execute("SELECT pg_advisory_xact_lock(724619830125)", connection, transaction, token);
        await Execute("CREATE TABLE IF NOT EXISTS battle_schema_migrations (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())", connection, transaction, token);
        await using var current = new NpgsqlCommand("SELECT COALESCE(MAX(version), 0) FROM battle_schema_migrations", connection, transaction);
        var version = Convert.ToInt32(await current.ExecuteScalarAsync(token));
        if (version > 1) throw new InvalidOperationException("Battle schema is newer than this application.");
        if (version == 0)
        {
            await using var stream = typeof(BattleDatabaseMigrator).Assembly.GetManifestResourceStream("BattleMigration001.sql")
                ?? throw new InvalidOperationException("Missing battle database migration.");
            using var reader = new StreamReader(stream);
            await Execute(await reader.ReadToEndAsync(token), connection, transaction, token);
            await Execute("INSERT INTO battle_schema_migrations (version) VALUES (1)", connection, transaction, token);
        }
        await transaction.CommitAsync(token);
    }
    private static async Task Execute(string sql, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(token);
    }
}
