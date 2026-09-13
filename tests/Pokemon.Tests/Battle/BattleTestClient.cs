using Pokemon.Domain.Battle.Repositories;
using Pokemon.Infrastructure.Persistence;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Tests.Battle;

/// <summary>
/// Adaptador de pruebas para ejercitar los repositorios SQL a través de la unidad de trabajo real.
/// </summary>
internal sealed class BattleTestClient(DatabaseConnectionFactory connections)
{
    private readonly DatabaseUnitOfWork transactions = new(connections);
    /// <summary>
    /// Guarda una partida mediante los repositorios SQL y la unidad de trabajo real de la prueba.
    /// </summary>
    public Task Add(BattleAggregate battle, CancellationToken token) => transactions.WriteAsync(async scope =>
    { await scope.GetRepository<IBattleRepository>().Add(battle, token); return true; }, token);
    /// <summary>
    /// Recupera una partida SQL mediante una sesión de lectura real.
    /// </summary>
    public Task<BattleAggregate> Get(Guid id, CancellationToken token) =>
        transactions.ReadAsync(scope => scope.GetReader<IBattleReader>().Get(id, token), token);
    /// <summary>
    /// Actualiza una partida SQL mediante una operación de la unidad de trabajo real.
    /// </summary>
    public Task<BattleAggregate> Update(Guid id, Func<BattleAggregate, BattleAggregate> action, CancellationToken token) =>
        transactions.WriteAsync(scope => scope.GetRepository<IBattleRepository>().Update(id, action, token), token);
}
