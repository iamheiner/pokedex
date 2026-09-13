using Pokemon.Domain.Pokedex.Exceptions;
using Pokemon.Domain.Common.Persistence;
using Pokemon.Tests.Persistence;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Infrastructure.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Pokedex;

public sealed class PokedexDomainTests
{
    private static readonly BaseStats Stats = new(40, 50, 60, 70, 80, 90);
    /// <summary>
    /// Construye una especie de prueba con cinco movimientos aprendibles a niveles consecutivos.
    /// </summary>
    private static Species Species() => new(Guid.NewGuid(), "Species", PokemonType.Fire, Stats,
        Enumerable.Range(1, 5).Select(i => new LearnableMove(Guid.NewGuid(), i)));

    /// <summary>
    /// Comprueba que el dominio rechaza potencias de movimiento fuera del intervalo permitido.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(251)]
    [InlineData(-1)]
    public void InvalidMovePowerCannotEnterDomain(int power) => Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.NewGuid(), "Move", power, PokemonType.Fire));
    /// <summary>
    /// Comprueba que el dominio rechaza niveles de aprendizaje fuera del intervalo permitido.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidLearningLevelCannotEnterDomain(int level) => Assert.Throws<PokedexRuleException>(() => new LearnableMove(Guid.NewGuid(), level));
    /// <summary>
    /// Comprueba que cada estadística base respeta los límites del dominio.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public void EveryStatMustRespectBounds(int invalid)
    {
        for (var index = 0; index < 6; index++)
        {
            var values = new[] { 40, 50, 60, 70, 80, 90 }; values[index] = invalid;
            Assert.Throws<PokedexRuleException>(() => new BaseStats(values[0], values[1], values[2], values[3], values[4], values[5]));
        }
    }
    /// <summary>
    /// Comprueba que las entidades rechazan identidades, nombres, tipos y planes de aprendizaje inválidos.
    /// </summary>
    [Fact]
    public void DomainRejectsMissingIdentityNameTypeAndDuplicateLearning()
    {
        Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.Empty, "Move", 40, PokemonType.Normal));
        Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.NewGuid(), " ", 40, PokemonType.Normal));
        Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.NewGuid(), "Move", 40, (PokemonType)100));
        var move = new LearnableMove(Guid.NewGuid(), 1);
        Assert.Throws<PokedexRuleException>(() => new Species(Guid.NewGuid(), "Species", PokemonType.Fire, Stats, [move, move]));
    }
    /// <summary>
    /// Comprueba que el dominio copia y protege sus colecciones frente a modificaciones externas.
    /// </summary>
    [Fact]
    public void CollectionsAreCopiedAndReadOnly()
    {
        var species = Species(); var entries = species.Learnset.ToArray();
        var copy = new Species(Guid.NewGuid(), "Copy", PokemonType.Fire, Stats, entries);
        entries[0] = new LearnableMove(Guid.NewGuid(), 1);
        Assert.NotEqual(entries[0], copy.Learnset[0]);
        var ids = species.Learnset.Take(4).Select(e => e.MoveId).ToArray();
        var pokemon = new OwnedPokemon(Guid.NewGuid(), species, "Name", 20, 0, 40, ids);
        ids[0] = Guid.NewGuid();
        Assert.NotEqual(ids[0], pokemon.MoveIds[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)pokemon.MoveIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LearnableMove>)copy.Learnset).Clear());
    }
    /// <summary>
    /// Comprueba que los comandos fallidos o cancelados no publican cambios parciales.
    /// </summary>
    [Fact]
    public async Task FailedAndCancelledTransactionsNeverPublishPartialChanges()
    {
        using var store = new InMemoryUnitOfWork(false);
        var move = new CatalogMove(Guid.NewGuid(), "Move", 40, PokemonType.Fire);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync<int>(async data => { await data.GetRepository<IMoveRepository>().SaveAsync(move, default); throw new InvalidOperationException(); }, default));
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(async data => { await data.GetRepository<IMoveRepository>().SaveAsync(move, default); cts.Cancel(); return 1; }, cts.Token));
        Assert.Empty(await store.ReadAsync(async data => await data.GetReader<IMoveReader>().ListAsync(new(), default), default));
        var called = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadAsync(async data => { called = true; return await data.GetReader<IMoveReader>().ListAsync(new(), default); }, cts.Token));
        Assert.False(called);
    }
    /// <summary>
    /// Comprueba que conservar referencias a sesiones terminadas no permite modificar el estado publicado.
    /// </summary>
    [Fact]
    public async Task EscapedSessionAndReaderCannotMutatePublishedState()
    {
        using var store = new InMemoryUnitOfWork(false);
        IRepositoryScope? escaped = null;
        var move = new CatalogMove(Guid.NewGuid(), "Move", 40, PokemonType.Fire);
        await store.WriteAsync(async data => { escaped = data; await data.GetRepository<IMoveRepository>().SaveAsync(move, default); return true; }, default);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => escaped!.GetRepository<IMoveRepository>().DeleteAsync(move.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(async data => { await ((IRepositoryScope)data).GetRepository<IMoveRepository>().DeleteAsync(move.Id, default); return true; }, default));
        Assert.Single(await store.ReadAsync(async data => await data.GetReader<IMoveReader>().ListAsync(new(), default), default));
    }
}
