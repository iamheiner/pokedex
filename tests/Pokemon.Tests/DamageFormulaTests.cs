using Pokemon.Domain;
namespace Pokemon.Tests;

public sealed class DamageFormulaTests
{
    [Theory]
    [InlineData(1, 37, 91, 13, 85, 0)]
    [InlineData(7, 83, 47, 65, 100, 11)]
    [InlineData(33, 121, 59, 75, 91, 42)]
    [InlineData(100, 10000, 1, 250, 100, 2100000)]
    [InlineData(1, 1, 10000, 1, 85, 0)]
    [InlineData(50, 49, 52, 40, 100, 16)]
    public void CalculatesWithAsymmetricStats(int level, int attack, int defense, int power, int random, int expected)
    {
        var move = new Move("Hit", power, PokemonType.Normal);
        var attacker = Create(level, attack, 1, move);
        var defender = Create(1, 1, defense);
        Assert.Equal(expected, DamageCalculator.Calculate(attacker, move, defender, random).Damage);
    }

    [Fact]
    public void RejectsUnlearnedMoveEvenWhenCalledWithoutApplication()
    {
        var move = new Move("Hit", 40, PokemonType.Normal);
        Assert.Throws<ArgumentException>(() => DamageCalculator.Calculate(Create(50, 52, 43), move, Create(50, 48, 65), 100));
    }

    [Fact]
    public void RejectsChangedPowerOfLearnedMove()
    {
        var learned = new Move("Hit", 40, PokemonType.Normal);
        var forged = new Move("Hit", 200, PokemonType.Normal);
        Assert.Throws<ArgumentException>(() => DamageCalculator.Calculate(Create(50, 52, 43, learned), forged, Create(50, 48, 65), 100));
    }

    [Fact]
    public void EqualMoveValuesAreRecognized()
    {
        var learned = new Move("Hit", 40, PokemonType.Normal);
        var equivalent = new Move("Hit", 40, PokemonType.Normal);
        Assert.Equal(14, DamageCalculator.Calculate(Create(50, 52, 43, learned), equivalent, Create(50, 48, 65), 100).Damage);
    }

    [Fact]
    public void EveryAllowedRandomFactorProducesMonotonicDamage()
    {
        var move = new Move("Hit", 65, PokemonType.Normal);
        var attacker = Create(37, 83, 47, move);
        var defender = Create(12, 31, 59);
        var previous = -1;
        for (var factor = 85; factor <= 100; factor++)
        {
            var result = DamageCalculator.Calculate(attacker, move, defender, factor);
            Assert.Equal(factor, result.RandomFactor);
            Assert.True(result.Damage >= previous);
            previous = result.Damage;
        }
    }

    private static Combatant Create(int level, int attack, int defense, params Move[] moves) =>
        new(Guid.NewGuid(), "Example", level, PokemonType.Normal, 100, 100, attack, defense, 71, 89, 55, moves);
}
