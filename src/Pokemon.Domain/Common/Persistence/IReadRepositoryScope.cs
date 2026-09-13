namespace Pokemon.Domain.Common.Persistence;

/// <summary>Resuelve lectores bajo la misma lectura coherente, solo durante el callback.</summary>
public interface IReadRepositoryScope
{
    TReader GetReader<TReader>() where TReader : class, IReadRepository;
}
