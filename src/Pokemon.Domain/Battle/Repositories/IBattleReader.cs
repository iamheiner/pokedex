using Pokemon.Domain.Common.Persistence;
namespace Pokemon.Domain.Battle.Repositories;

/// <summary>
/// Define la recuperación de partidas sin conceder operaciones de escritura.
/// </summary>
public interface IBattleReader : IReadRepository
{
    /// <summary>
    /// Recupera una partida por su identificador o informa de que no existe.
    /// </summary>
    Task<Battle> Get(Guid id, CancellationToken token);
}
