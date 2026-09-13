using Pokemon.Domain;
namespace Pokemon.Tests;

public sealed class DomainValidationTests
{
    /// <summary>Comprueba que un movimiento necesita un nombre válido.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MoveNeedsName(string? name) => Assert.ThrowsAny<ArgumentException>(() => new Move(name!, 40, PokemonType.Normal));

    /// <summary>Comprueba que la potencia fuera del intervalo permitido se rechaza.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(251)]
    public void MoveRejectsPowerOutsideContract(int power) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Move("Hit", power, PokemonType.Normal));

    /// <summary>Comprueba que los valores extremos permitidos de potencia son válidos.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    public void MoveAcceptsPowerBoundaries(int power) => Assert.Equal(power, new Move("Hit", power, PokemonType.Normal).Power);

    /// <summary>Comprueba que un movimiento no acepta tipos ajenos a la enumeración.</summary>
    [Fact]
    public void MoveRejectsUndefinedType() => Assert.Throws<ArgumentOutOfRangeException>(() => new Move("Hit", 40, (PokemonType)999));

    /// <summary>Comprueba que un participante rechaza niveles fuera del intervalo permitido.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void CombatantRejectsLevelOutsideContract(int level) => Assert.Throws<ArgumentOutOfRangeException>(() => Create(level: level));

    /// <summary>Comprueba que un participante acepta los niveles mínimo y máximo permitidos.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void CombatantAcceptsLevelBoundaries(int level) => Assert.Equal(level, Create(level: level).Level);

    /// <summary>Comprueba que la salud actual respeta los límites de su salud total.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void HealthMustFitItsTotal(int health) => Assert.Throws<ArgumentOutOfRangeException>(() => Create(health: health));

    /// <summary>Comprueba que la salud actual puede ser cero o igual a la salud total.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void HealthAcceptsBothBoundaries(int health) => Assert.Equal(health, Create(health: health).CurrentHealth);

    /// <summary>Comprueba los límites de cada estadística del participante.</summary>
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

    /// <summary>Comprueba que un participante requiere un identificador no vacío.</summary>
    [Fact]
    public void IdentityMustExist() => Assert.Throws<ArgumentException>(() =>
        new Combatant(Guid.Empty, "Example", 50, PokemonType.Normal, 1, 1, 1, 1, 1, 1, 1, []));

    /// <summary>Comprueba que un participante no acepta tipos ajenos a la enumeración.</summary>
    [Fact]
    public void CombatantRejectsUndefinedType() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new Combatant(Guid.NewGuid(), "Example", 50, (PokemonType)999, 1, 1, 1, 1, 1, 1, 1, []));

    /// <summary>Comprueba que un participante necesita un nombre válido.</summary>
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public void CombatantNeedsName(string? name) => Assert.ThrowsAny<ArgumentException>(() =>
        new Combatant(Guid.NewGuid(), name!, 50, PokemonType.Normal, 1, 1, 1, 1, 1, 1, 1, []));

    /// <summary>Comprueba que se admiten cuatro movimientos y que la colección queda protegida frente a cambios externos.</summary>
    [Fact]
    public void FourMovesAreAllowedAndCollectionCannotBeMutated()
    {
        var moves = Enumerable.Range(1, 4).Select(i => new Move($"Hit{i}", 40, PokemonType.Normal)).ToArray();
        var pokemon = Create(moves: moves);
        Assert.Equal(4, pokemon.Moves.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<Move>)pokemon.Moves).Add(moves[0]));
    }

    /// <summary>Comprueba que los nombres de movimientos duplicados se detectan sin distinguir mayúsculas.</summary>
    [Fact]
    public void DuplicateNamesIgnoreCase() => Assert.Throws<ArgumentException>(() =>
        Create(moves: [new Move("Hit", 40, PokemonType.Normal), new Move("hit", 50, PokemonType.Fire)]));

    /// <summary>Comprueba que un movimiento nulo no puede pertenecer al participante.</summary>
    [Fact]
    public void NullMoveIsInvalid() => Assert.Throws<ArgumentException>(() => Create(moves: [null!]));

    /// <summary>Comprueba que la colección de movimientos no puede ser nula.</summary>
    [Fact]
    public void NullCollectionIsInvalid() => Assert.Throws<ArgumentNullException>(() =>
        new Combatant(Guid.NewGuid(), "Example", 50, PokemonType.Normal, 1, 1, 1, 1, 1, 1, 1, null!));

    /// <summary>Comprueba que la calculadora rechaza participantes nulos.</summary>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CalculatorRejectsNullParticipants(int index)
    {
        var move = new Move("Hit", 40, PokemonType.Normal);
        var pokemon = Create(moves: [move]);
        Assert.Throws<ArgumentNullException>(() => DamageCalculator.Calculate(
            index == 0 ? null! : pokemon, index == 1 ? null! : move, index == 2 ? null! : pokemon, 100));
    }

    /// <summary>Construye un participante con valores válidos y parámetros configurables para probar sus límites.</summary>
    private static Combatant Create(int level = 50, int health = 100, Move[]? moves = null) =>
        new(Guid.NewGuid(), "Example", level, PokemonType.Normal, health, 100, 52, 43, 60, 50, 65, moves ?? []);
}
