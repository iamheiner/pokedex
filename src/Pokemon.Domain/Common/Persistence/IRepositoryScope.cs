namespace Pokemon.Domain.Common.Persistence;

/// <summary>
/// Resuelve bajo demanda los repositorios que comparten una transacción de escritura.
/// </summary>
public interface IRepositoryScope : IReadRepositoryScope
{
    /// <summary>
    /// Obtiene el repositorio de escritura solicitado dentro de la transacción actual.
    /// </summary>
    TRepository GetRepository<TRepository>() where TRepository : class, IWriteRepository;
}
