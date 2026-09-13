namespace Pokemon.Domain.Common.Persistence;

/// <summary>Resuelve bajo demanda los repositorios que comparten una transacción de escritura.</summary>
public interface IRepositoryScope : IReadRepositoryScope
{
    TRepository GetRepository<TRepository>() where TRepository : class, IWriteRepository;
}
