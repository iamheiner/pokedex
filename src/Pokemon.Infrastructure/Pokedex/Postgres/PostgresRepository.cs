using Npgsql;
namespace Pokemon.Infrastructure.Pokedex.Postgres;

/// <summary>
/// Seguimiento de cambios de una transacción SQL. El diccionario es el snapshot de trabajo,
/// no el almacenamiento definitivo. Solo Flush escribe en la conexión y transacción recibidas.
/// </summary>
internal abstract class PostgresRepository<T>(Func<T, Guid> identity)
{
    protected readonly Dictionary<Guid, T> Entries = [];
    private readonly HashSet<Guid> changed = [];
    private readonly HashSet<Guid> deleted = [];
    public IReadOnlyCollection<T> List() => Entries.Values.ToArray();
    public T? Find(Guid id) => Entries.GetValueOrDefault(id);
    public void Save(T aggregate) { var id = identity(aggregate); Entries[id] = aggregate; changed.Add(id); deleted.Remove(id); }
    public void Delete(Guid id) { Entries.Remove(id); changed.Remove(id); deleted.Add(id); }
    public async Task Flush(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        foreach (var id in deleted) await DeleteRow(id, connection, transaction, token);
        foreach (var id in changed) await SaveRow(Entries[id], connection, transaction, token);
    }
    protected abstract Task DeleteRow(Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token);
    protected abstract Task SaveRow(T aggregate, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token);
    internal static async Task Execute(string sql, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(token);
    }
}
