namespace Pokemon.Domain.Battle.Repositories;

/// <summary>Recupera una partida sin conceder operaciones para crearla o ejecutar turnos.</summary>
public interface IBattleReader
{
    Task<Battle> Get(Guid id, CancellationToken token);
}
