namespace Pokemon.Infrastructure.Persistence;

/// <summary>Bloquea el catálogo solo si una escritura lo consulta. Las partidas no adquieren este bloqueo.</summary>
internal sealed class CatalogDatabaseSession(DatabaseSession database, bool readOnly)
{
    private bool locked;
    private async Task PrepareAsync(CancellationToken token)
    {
        database.EnsureActive();
        if (readOnly || locked) return;
        // Antes de la primera consulta: las comprobaciones ven el último commit del escritor anterior.
        await database.ExecuteAsync("SELECT pg_advisory_xact_lock(724619830127)", null, token);
        locked = true;
    }
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? args, CancellationToken token)
    { await PrepareAsync(token); return await database.QueryAsync<T>(sql, args, token); }
    public async Task<T> ScalarAsync<T>(string sql, object? args, CancellationToken token)
    { await PrepareAsync(token); return await database.ScalarAsync<T>(sql, args, token); }
    public async Task<int> ExecuteAsync(string sql, object? args, CancellationToken token)
    { await PrepareAsync(token); return await database.ExecuteAsync(sql, args, token); }
}
