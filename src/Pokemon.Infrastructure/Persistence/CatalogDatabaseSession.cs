namespace Pokemon.Infrastructure.Persistence;

/// <summary>Bloquea el catálogo solo si una escritura lo consulta. Las partidas no adquieren este bloqueo.</summary>
internal sealed class CatalogDatabaseSession(DatabaseSession database, bool readOnly)
{
    private bool locked;
    /// <summary>Comprueba la sesión y adquiere una sola vez el bloqueo del catálogo si la operación permite escritura.</summary>
    private async Task PrepareAsync(CancellationToken token)
    {
        database.EnsureActive();
        if (readOnly || locked) return;
        // Antes de la primera consulta: las comprobaciones ven el último commit del escritor anterior.
        await database.ExecuteAsync("SELECT pg_advisory_xact_lock(724619830127)", null, token);
        locked = true;
    }
    /// <summary>Consulta las filas del catálogo después de preparar su acceso transaccional.</summary>
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? args, CancellationToken token)
    { await PrepareAsync(token); return await database.QueryAsync<T>(sql, args, token); }
    /// <summary>Obtiene un valor escalar del catálogo después de preparar su acceso transaccional.</summary>
    public async Task<T> ScalarAsync<T>(string sql, object? args, CancellationToken token)
    { await PrepareAsync(token); return await database.ScalarAsync<T>(sql, args, token); }
    /// <summary>Ejecuta una instrucción sobre el catálogo y devuelve las filas afectadas dentro de la transacción.</summary>
    public async Task<int> ExecuteAsync(string sql, object? args, CancellationToken token)
    { await PrepareAsync(token); return await database.ExecuteAsync(sql, args, token); }
}
