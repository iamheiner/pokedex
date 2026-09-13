using Pokemon.Domain;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Battle.Exceptions;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Persistence;
using Pokemon.Tests.Battle;
namespace Pokemon.Tests.Persistence;

[Trait("Category", "Postgres")]
public sealed class GenericUnitOfWorkTests
{
    private interface IProbeReader : IReadRepository;
    private interface IProbeWriter : IWriteRepository;
    private sealed class ProbeRepository : IProbeReader, IProbeWriter;

    /// <summary>Comprueba la creación diferida, la reutilización por operación y la extensión del registro de repositorios.</summary>
    [PostgresFact]
    public async Task Repositories_are_lazy_reused_within_operation_and_extensible_by_registration()
    {
        await using var db = await TestDatabase.Create();
        var created = 0;
        var registry = RepositoryRegistry.CreateDefault().Register(_ =>
        { created++; return new ProbeRepository(); }, typeof(IProbeReader), typeof(IProbeWriter));
        var unit = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source), registry);
        IProbeReader? first = null;
        await unit.WriteAsync(scope =>
        {
            Assert.Equal(0, created);
            first = scope.GetReader<IProbeReader>();
            Assert.Same(first, scope.GetReader<IProbeReader>());
            Assert.Same(first, scope.GetRepository<IProbeWriter>());
            Assert.Equal(1, created);
            return Task.FromResult(true);
        }, default);
        await unit.ReadAsync(scope =>
        {
            Assert.Equal(1, created);
            Assert.NotSame(first, scope.GetReader<IProbeReader>());
            Assert.Equal(2, created);
            return Task.FromResult(true);
        }, default);
    }

    /// <summary>Comprueba que los cambios de catálogo y partida se confirman o revierten en una misma transacción.</summary>
    [PostgresFact]
    public async Task Catalog_and_battle_commit_or_roll_back_in_the_same_transaction()
    {
        await using var db = await TestDatabase.Create();
        await new Pokemon.Infrastructure.Persistence.Migrations.PokedexDatabaseMigrator(new DatabaseConnectionFactory(db.Source)).Migrate(default);
        var unit = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        foreach (var fail in new[] { true, false })
        {
            var move = new CatalogMove(Guid.NewGuid(), "Atomic " + Guid.NewGuid(), 40, PokemonType.Normal);
            var battle = BattleDomainTests.Duel();
            // Guarda el movimiento y la partida de prueba y simula un fallo cuando el escenario exige revertir ambos.
            Task Execute() => unit.WriteAsync(async scope =>
            {
                await scope.GetRepository<IMoveRepository>().SaveAsync(move, default);
                await scope.GetRepository<IBattleRepository>().Add(battle, default);
                if (fail) throw new InvalidOperationException("Abort both modules");
                return true;
            }, default);
            if (fail) await Assert.ThrowsAsync<InvalidOperationException>(Execute);
            else await Execute();
            var savedMove = await unit.ReadAsync(scope => scope.GetReader<IMoveReader>().FindAsync(move.Id, default), default);
            if (fail)
            {
                Assert.Null(savedMove);
                await Assert.ThrowsAsync<BattleNotFoundException>(() => unit.ReadAsync(scope => scope.GetReader<IBattleReader>().Get(battle.Id, default), default));
            }
            else
            {
                Assert.NotNull(savedMove);
                Assert.Equal((move.Id, move.Name, move.Power, move.Type), (savedMove.Id, savedMove.Name, savedMove.Power, savedMove.Type));
                Assert.Equal(battle.Id, (await unit.ReadAsync(scope => scope.GetReader<IBattleReader>().Get(battle.Id, default), default)).Id);
            }
        }
    }

    /// <summary>Comprueba que una sesión de lectura rechaza escritores y que una sesión cerrada no resuelve repositorios.</summary>
    [PostgresFact]
    public async Task Read_scope_cannot_resolve_writers_even_after_explicit_cast_and_closed_scope_rejects_resolution()
    {
        await using var db = await TestDatabase.Create();
        var unit = new DatabaseUnitOfWork(new DatabaseConnectionFactory(db.Source));
        IReadRepositoryScope? escaped = null;
        await unit.ReadAsync(scope =>
        {
            escaped = scope;
            Assert.Throws<InvalidOperationException>(() => ((IRepositoryScope)scope).GetRepository<IBattleRepository>());
            return Task.FromResult(true);
        }, default);
        Assert.Throws<ObjectDisposedException>(() => escaped!.GetReader<IBattleReader>());
    }
}
