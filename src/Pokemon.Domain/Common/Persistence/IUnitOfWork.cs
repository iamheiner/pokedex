namespace Pokemon.Domain.Common.Persistence;

/// <summary>
/// Coordina una operación completa, con independencia del agregado. Los repositorios se
/// resuelven cuando se necesitan y comparten transacción. Confirma todo o revierte todo.
/// El callback debe esperar sus llamadas secuencialmente y no conservar repositorios fuera de él.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Ejecuta un comando y confirma o revierte conjuntamente las operaciones de sus repositorios.
    /// </summary>
    Task<T> WriteAsync<T>(Func<IRepositoryScope, Task<T>> command, CancellationToken token);
}
