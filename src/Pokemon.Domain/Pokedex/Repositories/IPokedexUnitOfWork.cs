namespace Pokemon.Domain.Pokedex.Repositories;

/// <summary>
/// Confirma o revierte todas las operaciones de sus repositorios. El callback es asíncrono
/// y carga exclusivamente los agregados que necesita. Los repositorios no pueden usarse
/// fuera de la operación; la implementación controla el cierre de la sesión.
/// </summary>
public interface IPokedexUnitOfWork
{
    Task<T> WriteAsync<T>(Func<IPokedexSession, Task<T>> command, CancellationToken token);
}
