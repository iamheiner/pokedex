using Pokemon.Tests.Persistence;
using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
using Pokemon.Infrastructure.Pokedex;
using Pokemon.Domain.Pokedex.Repositories;
namespace Pokemon.Tests.Pokedex;

public sealed class PokedexDomainTests
{
    private static readonly BaseStats Stats = new(40, 50, 60, 70, 80, 90);
    private static Species Species() => new(Guid.NewGuid(), "Species", PokemonType.Fire, Stats,
        Enumerable.Range(1, 5).Select(i => new LearnableMove(Guid.NewGuid(), i)));

    [Theory]
    [InlineData(0)] [InlineData(251)] [InlineData(-1)]
    public void InvalidMovePowerCannotEnterDomain(int power) => Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.NewGuid(), "Move", power, PokemonType.Fire));
    [Theory]
    [InlineData(0)] [InlineData(101)]
    public void InvalidLearningLevelCannotEnterDomain(int level) => Assert.Throws<PokedexRuleException>(() => new LearnableMove(Guid.NewGuid(), level));
    [Theory]
    [InlineData(0)] [InlineData(10001)]
    public void EveryStatMustRespectBounds(int invalid)
    {
        for (var index = 0; index < 6; index++)
        {
            var values = new[] {40, 50, 60, 70, 80, 90}; values[index] = invalid;
            Assert.Throws<PokedexRuleException>(() => new BaseStats(values[0], values[1], values[2], values[3], values[4], values[5]));
        }
    }
    [Fact]
    public void DomainRejectsMissingIdentityNameTypeAndDuplicateLearning()
    {
        Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.Empty, "Move", 40, PokemonType.Normal));
        Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.NewGuid(), " ", 40, PokemonType.Normal));
        Assert.Throws<PokedexRuleException>(() => new CatalogMove(Guid.NewGuid(), "Move", 40, (PokemonType)100));
        var move = new LearnableMove(Guid.NewGuid(), 1);
        Assert.Throws<PokedexRuleException>(() => new Species(Guid.NewGuid(), "Species", PokemonType.Fire, Stats, [move, move]));
    }
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
    [Fact]
    public async Task FailedAndCancelledTransactionsNeverPublishPartialChanges()
    {
        using var store = new InMemoryPokedexUnitOfWork(false);
        var move = new CatalogMove(Guid.NewGuid(), "Move", 40, PokemonType.Fire);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Write<int>(data => { data.MoveRepository.Save(move); throw new InvalidOperationException(); }, default));
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Write(data => { data.MoveRepository.Save(move); cts.Cancel(); return 1; }, cts.Token));
        Assert.Empty(await store.Read(data => data.Moves, default));
        var called = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Read(data => { called = true; return data.Moves; }, cts.Token));
        Assert.False(called);
    }
    [Fact]
    public async Task EscapedSessionAndReaderCannotMutatePublishedState()
    {
        using var store = new InMemoryPokedexUnitOfWork(false);
        IPokedexSession? escaped = null;
        var move = new CatalogMove(Guid.NewGuid(), "Move", 40, PokemonType.Fire);
        await store.Write(data => { escaped = data; data.MoveRepository.Save(move); return true; }, default);
        escaped!.MoveRepository.Delete(move.Id);
        await store.Read(data => { ((IPokedexSession)data).MoveRepository.Delete(move.Id); return true; }, default);
        Assert.Single(await store.Read(data => data.Moves, default));
    }
}
