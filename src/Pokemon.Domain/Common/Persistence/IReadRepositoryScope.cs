namespace Pokemon.Domain.Common.Persistence;

/// <summary>
/// Resuelve lectores bajo la misma lectura coherente, solo durante el callback.
/// </summary>
public interface IReadRepositoryScope
{
    /// <summary>
    /// Obtiene el lector solicitado dentro de la operación de lectura actual.
    /// </summary>
    TReader GetReader<TReader>() where TReader : class, IReadRepository;
}
