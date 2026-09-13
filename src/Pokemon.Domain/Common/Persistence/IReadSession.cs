namespace Pokemon.Domain.Common.Persistence;

/// <summary>
/// Ejecuta una consulta coherente. Su callback solo recibe lectores.
/// </summary>
public interface IReadSession
{
    /// <summary>
    /// Ejecuta una consulta coherente con acceso exclusivamente a contratos de lectura.
    /// </summary>
    Task<T> ReadAsync<T>(Func<IReadRepositoryScope, Task<T>> query, CancellationToken token);
}
