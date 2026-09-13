namespace Pokemon.Domain.Common.Persistence;

/// <summary>Ejecuta una consulta coherente. Su callback solo recibe lectores.</summary>
public interface IReadSession
{
    Task<T> ReadAsync<T>(Func<IReadRepositoryScope, Task<T>> query, CancellationToken token);
}
