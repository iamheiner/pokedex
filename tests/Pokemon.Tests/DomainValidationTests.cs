using Pokemon.Domain;
namespace Pokemon.Tests;

public sealed class DomainValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MoveNeedsName(string? name) => Assert.ThrowsAny<ArgumentException>(() => new Move(name!, 40, PokemonType.Normal));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(251)]
    public void MoveRejectsPowerOutsideContract(int power) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Move("Hit", power, PokemonType.Normal));

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    public void MoveAcceptsPowerBoundaries(int power) => Assert.Equal(power, new Move("Hit", power, PokemonType.Normal).Power);

    [Fact]
    public void MoveRejectsUndefinedType() => Assert.Throws<ArgumentOutOfRangeException>(() => new Move("Hit", 40, (PokemonType)999));

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void CombatantRejectsLevelOutsideContract(int level) => Assert.Throws<ArgumentOutOfRangeException>(() => Create(level: level));

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void CombatantAcceptsLevelBoundaries(int level) => Assert.Equal(level, Create(level: level).Level);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void HealthMustFitItsTotal(int health) => Assert.Throws<ArgumentOutOfRangeException>(() => Create(health: health));

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void HealthAcceptsBothBoundaries(int health) => Assert.Equal(health, Create(health: health).CurrentHealth);

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 0)] [InlineData(2, 0)]
    [InlineData(3, 0)] [InlineData(4, 0)] [InlineData(5, 0)]
    [InlineData(0, 10001)] [InlineData(1, 10001)] [InlineData(2, 10001)]
    [InlineData(3, 10001)] [InlineData(4, 10001)] [InlineData(5, 10001)]
    public void EveryStatHasBounds(int index, int value)
    {
        var stats = new[] { 100, 100, 100, 100, 100, 100 }; stats[index] = value;
        Assert.ThrowsAny<ArgumentException>(() => new Combatant(Guid.NewGuid(), "Example", 50,
            PokemonType.Normal, 0, stats[0], stats[1], stats[2], stats[3], stats[4], stats[5], []));
    }

    [Fact]
    public void IdentityMustExist() => Assert.Throws<ArgumentException>(() =>
        new Combatant(Guid.Empty, "Example", 50, PokemonType.Normal, 1, 1, 1, 1, 1, 1, 1, []));

    [Fact]
    public void CombatantRejectsUndefinedType() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new Combatant(Guid.NewGuid(), "Example", 50, (PokemonType)999, 1, 1, 1, 1, 1, 1, 1, []));

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public void CombatantNeedsName(string? name) => Assert.ThrowsAny<ArgumentException>(() =>
        new Combatant(Guid.NewGuid(), name!, 50, PokemonType.Normal, 1, 1, 1, 1, 1, 1, 1, []));

    [Fact]
    public void FourMovesAreAllowedAndCollectionCannotBeMutated()
    {
        var moves = Enumerable.Range(1, 4).Select(i => new Move($"Hit{i}", 40, PokemonType.Normal)).ToArray();
        var pokemon = Create(moves: moves);
        Assert.Equal(4, pokemon.Moves.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<Move>)pokemon.Moves).Add(moves[0]));
    }

    [Fact]
    public void DuplicateNamesIgnoreCase() => Assert.Throws<ArgumentException>(() =>
        Create(moves: [new Move("Hit", 40, PokemonType.Normal), new Move("hit", 50, PokemonType.Fire)]));

    [Fact]
    public void NullMoveIsInvalid() => Assert.Throws<ArgumentException>(() => Create(moves: [null!]));

    [Fact]
    public void NullCollectionIsInvalid() => Assert.Throws<ArgumentNullException>(() =>
        new Combatant(Guid.NewGuid(), "Example", 50, PokemonType.Normal, 1, 1, 1, 1, 1, 1, 1, null!));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CalculatorRejectsNullParticipants(int index)
    {
        var move = new Move("Hit", 40, PokemonType.Normal);
        var pokemon = Create(moves: [move]);
        Assert.Throws<ArgumentNullException>(() => DamageCalculator.Calculate(
            index == 0 ? null! : pokemon, index == 1 ? null! : move, index == 2 ? null! : pokemon, 100));
    }

    private static Combatant Create(int level = 50, int health = 100, Move[]? moves = null) =>
        new(Guid.NewGuid(), "Example", level, PokemonType.Normal, health, 100, 52, 43, 60, 50, 65, moves ?? []);
}
