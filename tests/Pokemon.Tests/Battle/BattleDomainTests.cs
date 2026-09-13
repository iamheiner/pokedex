using Pokemon.Tests.Persistence;
using Pokemon.Domain;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Battle;
using BattleAggregate = Pokemon.Domain.Battle.Battle;
namespace Pokemon.Tests.Battle;

public sealed class BattleDomainTests
{
    internal static BattlePokemon Fighter(int speed = 50, int health = 100, int total = 100,
        PokemonType type = PokemonType.Normal, PokemonType moveType = PokemonType.Normal,
        int level = 50, int attack = 100, int defense = 100, int power = 100)
    {
        var moves = Enumerable.Range(1, 4).Select(n => new BattleMove(Guid.NewGuid(), new Move($"Move {n}", power, moveType))).ToArray();
        var snapshot = new Combatant(Guid.NewGuid(), "Fighter", level, type, health, total, attack, defense, 100, 100, speed, moves.Select(m => m.Definition));
        return new(snapshot, moves);
    }
    internal static BattleAggregate Duel(int health = 100, int total = 100) =>
        BattleAggregate.Start(Guid.NewGuid(), Fighter(health: health, total: total, type: PokemonType.Ghost),
            Fighter(health: health, total: total, type: PokemonType.Ghost));
    internal static BattleAggregate Next(BattleAggregate battle)
    {
        var actor = battle.NextPokemonId == battle.First.Snapshot.Id ? battle.First : battle.Second;
        return battle.PlayTurn(actor.Snapshot.Id, actor.Moves.FirstOrDefault(m => m.RemainingUses > 0)?.Id, battle.Version, 100);
    }

    [Theory]
    [InlineData(40, 80, false)] [InlineData(80, 40, true)] [InlineData(50, 50, true)]
    public void SpeedSelectsFirstActorAndRequestOrderBreaksTie(int firstSpeed, int secondSpeed, bool firstStarts)
    {
        var first = Fighter(speed: firstSpeed); var second = Fighter(speed: secondSpeed);
        var battle = BattleAggregate.Start(Guid.NewGuid(), first, second);
        Assert.Equal(firstStarts ? first.Snapshot.Id : second.Snapshot.Id, battle.NextPokemonId);
        Assert.Equal(1, battle.Version); Assert.Empty(battle.Turns); Assert.Null(battle.WinnerId);
        Assert.Equal(BattlePhase.AwaitingAction, battle.Phase);
    }
    [Fact]
    public void CreationRejectsSamePokemonMissingIdentityAndFaintedParticipants()
    {
        var fighter = Fighter();
        Assert.Throws<BattleRuleException>(() => BattleAggregate.Start(Guid.NewGuid(), fighter, fighter));
        Assert.Throws<BattleRuleException>(() => BattleAggregate.Start(Guid.Empty, fighter, Fighter()));
        Assert.Throws<BattleRuleException>(() => BattleAggregate.Start(Guid.NewGuid(), Fighter(health: 0), fighter));
        Assert.Throws<BattleRuleException>(() => BattleAggregate.Start(Guid.NewGuid(), fighter, Fighter(health: 0)));
        Assert.Throws<ArgumentNullException>(() => BattleAggregate.Start(Guid.NewGuid(), null!, fighter));
        Assert.Throws<ArgumentNullException>(() => BattleAggregate.Start(Guid.NewGuid(), fighter, null!));
    }
    [Fact]
    public void NormalTurnReusesMoveTypeFormulaAndAdvancesOnlyItsOwnState()
    {
        var first = Fighter(type: PokemonType.Water, moveType: PokemonType.Fire);
        var second = Fighter(type: PokemonType.Grass);
        var battle = BattleAggregate.Start(Guid.NewGuid(), first, second);
        var next = battle.PlayTurn(first.Snapshot.Id, first.Moves[0].Id, 1, 100);
        var turn = Assert.Single(next.Turns);
        Assert.Equal(88, turn.CalculatedDamage); Assert.Equal(88, turn.AppliedDamage);
        Assert.Equal(2m, turn.Effectiveness); Assert.Equal(100, turn.RandomFactor);
        Assert.Equal(12, next.Second.Snapshot.CurrentHealth); Assert.Equal(100, next.First.Snapshot.CurrentHealth);
        Assert.Equal(4, next.First.Moves[0].RemainingUses); Assert.Equal(second.Snapshot.Id, next.NextPokemonId);
        Assert.Equal(2, next.Version); Assert.Equal(1, turn.Number);
        Assert.Equal(100, battle.Second.Snapshot.CurrentHealth); Assert.Equal(5, battle.First.Moves[0].RemainingUses);
        Assert.Empty(battle.Turns);
    }
    [Fact]
    public void LethalDamageIsClampedAndFinishesBeforeOpponentCanAct()
    {
        var first = Fighter(); var second = Fighter(health: 10);
        var battle = BattleAggregate.Start(Guid.NewGuid(), first, second);
        var finished = Next(battle);
        Assert.Equal(44, finished.Turns[0].CalculatedDamage); Assert.Equal(10, finished.Turns[0].AppliedDamage);
        Assert.Equal(0, finished.Second.Snapshot.CurrentHealth); Assert.Equal(first.Snapshot.Id, finished.WinnerId);
        Assert.Null(finished.NextPokemonId); Assert.False(finished.IsDraw); Assert.Equal(BattlePhase.Finished, finished.Phase);
        Assert.Throws<BattleConflictException>(() => finished.PlayTurn(second.Snapshot.Id, second.Moves[0].Id, finished.Version, 100));
    }
    [Fact]
    public void InvalidActorMoveVersionAndPrematureStruggleAreRejected()
    {
        var battle = Duel(); var actor = battle.First;
        Assert.Throws<BattleConflictException>(() => battle.PlayTurn(battle.Second.Snapshot.Id, battle.Second.Moves[0].Id, 1, 100));
        Assert.Throws<BattleConflictException>(() => battle.PlayTurn(Guid.NewGuid(), actor.Moves[0].Id, 1, 100));
        Assert.Throws<BattleConflictException>(() => battle.PlayTurn(actor.Snapshot.Id, Guid.NewGuid(), 1, 100));
        Assert.Throws<BattleConflictException>(() => battle.PlayTurn(actor.Snapshot.Id, actor.Moves[0].Id, 2, 100));
        Assert.Throws<BattleConflictException>(() => battle.PlayTurn(actor.Snapshot.Id, null, 1, 100));
        Assert.Empty(battle.Turns);
    }
    [Theory]
    [InlineData(84)] [InlineData(101)]
    public void InvalidRandomFactorIsAnInternalFailure(int factor)
    {
        var battle = Duel();
        Assert.Throws<ArgumentOutOfRangeException>(() => battle.PlayTurn(battle.First.Snapshot.Id, battle.First.Moves[0].Id, 1, factor));
        Assert.Equal(1, battle.Version);
    }
    [Fact]
    public void ImmunityStillConsumesUsesAndExhaustedMoveCannotBeSelected()
    {
        var battle = Duel();
        for (var n = 0; n < 10; n++) battle = Next(battle);
        Assert.All(battle.Turns, turn => { Assert.Equal(0, turn.AppliedDamage); Assert.Equal(0m, turn.Effectiveness); });
        Assert.Equal(0, battle.First.Moves[0].RemainingUses);
        Assert.Throws<BattleConflictException>(() => battle.PlayTurn(battle.First.Snapshot.Id, battle.First.Moves[0].Id, battle.Version, 100));
        Assert.Throws<BattleRuleException>(() => BattleAggregate.Start(Guid.NewGuid(), battle.First, Fighter()));
    }
    [Theory]
    [InlineData(1)] [InlineData(9)] [InlineData(10)] [InlineData(19)] [InlineData(100)] [InlineData(10000)]
    public void MutualImmunityAlwaysEndsEvenForRoundingBoundaries(int health)
    {
        var battle = Duel(health, health);
        while (battle.Phase != BattlePhase.Finished && battle.Turns.Count < 60) battle = Next(battle);
        Assert.Equal(BattlePhase.Finished, battle.Phase); Assert.True(battle.IsDraw); Assert.Null(battle.WinnerId);
        Assert.Equal(40, battle.Turns.Count(t => !t.IsStruggle));
        Assert.All(battle.Turns.Where(t => t.IsStruggle), t =>
        {
            Assert.True(t.AppliedDamage > 0); Assert.True(t.RecoilDamage > 0);
            Assert.Null(t.RandomFactor); Assert.Null(t.Effectiveness);
        });
        Assert.Equal(0, battle.First.Snapshot.CurrentHealth); Assert.Equal(0, battle.Second.Snapshot.CurrentHealth);
    }
    [Fact]
    public void ZeroDamageFromRoundingAlsoEndsViaStruggle()
    {
        var battle = BattleAggregate.Start(Guid.NewGuid(),
            Fighter(health: 1, total: 1, level: 1, attack: 1, defense: 10000, power: 1),
            Fighter(health: 1, total: 1, level: 1, attack: 1, defense: 10000, power: 1));
        for (var n = 0; n < 41; n++) battle = Next(battle);
        Assert.True(battle.IsDraw); Assert.Equal(1, battle.Turns[^1].AppliedDamage);
        Assert.All(battle.Turns.Take(40), t => { Assert.Equal(0, t.AppliedDamage); Assert.Equal(1m, t.Effectiveness); });
    }
    [Theory]
    [InlineData(1, 100, false)] [InlineData(100, 1, true)]
    public void StruggleCanLoseThroughRecoilOrWinThroughDamage(int firstHealth, int secondHealth, bool firstWins)
    {
        var battle = BattleAggregate.Start(Guid.NewGuid(), Fighter(health: firstHealth, type: PokemonType.Ghost), Fighter(health: secondHealth, type: PokemonType.Ghost));
        for (var n = 0; n < 41; n++) battle = Next(battle);
        Assert.Equal(firstWins ? battle.First.Snapshot.Id : battle.Second.Snapshot.Id, battle.WinnerId);
        Assert.False(battle.IsDraw);
    }
    [Fact]
    public void HistoryAndMovesAreReadOnlyAndMismatchedSnapshotsAreRejected()
    {
        var battle = Next(Duel());
        Assert.Throws<NotSupportedException>(() => ((IList<BattleTurn>)battle.Turns).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<BattleMove>)battle.First.Moves).Clear());
        Assert.Throws<BattleRuleException>(() => new BattlePokemon(battle.First.Snapshot, []));
        Assert.Throws<BattleRuleException>(() => new BattlePokemon(battle.First.Snapshot, Fighter().Moves.Select(m => new BattleMove(m.Id, new Move("Other", 10, PokemonType.Normal)))));
        Assert.Throws<BattleRuleException>(() => new BattleMove(Guid.Empty, new Move("Move", 10, PokemonType.Normal)));
    }
    [Fact]
    public async Task StoreRollsBackExceptionsCancellationAndInvalidVersionAdvances()
    {
        using var store = new InMemoryBattleRepository(); var battle = Duel(); await store.Add(battle, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Update(battle.Id, b => throw new InvalidOperationException(), default));
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Update(battle.Id, b => { var next = Next(b); cts.Cancel(); return next; }, cts.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Update(battle.Id, b => b, default));
        Assert.Same(battle, await store.Get(battle.Id, default));
        await Assert.ThrowsAsync<BattleConflictException>(() => store.Add(battle, default));
        await Assert.ThrowsAsync<BattleNotFoundException>(() => store.Get(Guid.NewGuid(), default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Get(battle.Id, cts.Token));
    }
}
