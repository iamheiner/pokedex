using Pokemon.Application.Common.Exceptions;
using Pokemon.Application.Feature.Damage;
using Pokemon.Application.Feature.Damage.Queries.CalculateDamage;
using Pokemon.Domain;

namespace Pokemon.Tests;

public class DamageTests
{
    /// <summary>
    /// Construye un participante de tipo configurable con estadísticas fijas para las pruebas de daño.
    /// </summary>
    private static Combatant Create(PokemonType type, params Move[] moves) =>
        new(Guid.NewGuid(), "Example", 50, type, 100, 100, 100, 100, 100, 100, 100, moves);

    /// <summary>
    /// Comprueba que se usa el tipo del movimiento y que el daño se redondea hacia abajo.
    /// </summary>
    [Theory]
    [InlineData(PokemonType.Grass, 100, 88)]
    [InlineData(PokemonType.Grass, 85, 74)]
    [InlineData(PokemonType.Water, 100, 22)]
    [InlineData(PokemonType.Normal, 100, 44)]
    public void UsesMoveTypeAndRoundsDown(PokemonType defenderType, int factor, int expected)
    {
        var move = new Move("Flame", 100, PokemonType.Fire);
        var attacker = Create(PokemonType.Water, move);
        var defender = Create(defenderType);
        var result = DamageCalculator.Calculate(attacker, move, defender, factor);
        Assert.Equal(expected, result.Damage);
        Assert.Equal(100, defender.CurrentHealth);
    }

    /// <summary>
    /// Comprueba que una inmunidad del defensor produce daño cero.
    /// </summary>
    [Fact]
    public void ImmunityProducesZeroDamage()
    {
        var move = new Move("Thunder", 100, PokemonType.Electric);
        Assert.Equal(0, DamageCalculator.Calculate(Create(PokemonType.Electric, move), move,
            Create(PokemonType.Ground), 100).Damage);
    }

    /// <summary>
    /// Comprueba que se rechazan factores aleatorios fuera del intervalo permitido.
    /// </summary>
    [Theory]
    [InlineData(84)]
    [InlineData(101)]
    public void RejectsInvalidRandomFactor(int factor)
    {
        var move = new Move("Hit", 100, PokemonType.Normal);
        Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Calculate(
            Create(PokemonType.Normal, move), move, Create(PokemonType.Normal), factor));
    }

    /// <summary>
    /// Comprueba que se rechaza un movimiento que el atacante no conoce.
    /// </summary>
    [Fact]
    public async Task RejectsUnknownMove()
    {
        var handler = new CalculateDamageQueryHandler(new FixedRandom());
        await Assert.ThrowsAsync<InvalidDamageRequestException>(() => handler.Handle(
            new CalculateDamageQuery(Create(PokemonType.Fire), "Flame", Create(PokemonType.Grass)), CancellationToken.None));
    }

    /// <summary>
    /// Comprueba que el caso de uso utiliza el proveedor aleatorio inyectado.
    /// </summary>
    [Fact]
    public async Task ApplicationUsesInjectedRandom()
    {
        var move = new Move("Flame", 100, PokemonType.Fire);
        var result = await new CalculateDamageQueryHandler(new FixedRandom()).Handle(
            new CalculateDamageQuery(Create(PokemonType.Water, move), "flame", Create(PokemonType.Grass)), CancellationToken.None);
        Assert.Equal(74, result.Damage);
        Assert.Equal(85, result.RandomFactor);
    }

    /// <summary>
    /// Comprueba que la colección de movimientos del agregado no puede modificarse desde fuera.
    /// </summary>
    [Fact]
    public void AggregateProtectsItsMoveCollection()
    {
        var original = new[] { new Move("Hit", 10, PokemonType.Normal) };
        var pokemon = Create(PokemonType.Normal, original);
        original[0] = new Move("Other", 20, PokemonType.Fire);
        Assert.Equal("Hit", pokemon.Moves[0].Name);
        Assert.Throws<ArgumentException>(() => Create(PokemonType.Normal,
            Enumerable.Range(0, 5).Select(i => new Move($"Move{i}", 10, PokemonType.Normal)).ToArray()));
        Assert.Throws<ArgumentException>(() => Create(PokemonType.Normal, original[0], original[0]));
    }

    /// <summary>
    /// Comprueba que el dominio rechaza una defensa igual a cero.
    /// </summary>
    [Fact]
    public void RejectsZeroDefense()
    {
        Assert.Throws<ArgumentException>(() => new Combatant(Guid.NewGuid(), "Example", 50,
            PokemonType.Normal, 100, 100, 100, 0, 100, 100, 100, []));
    }

    private sealed class FixedRandom : IDamageRandom
    {
        /// <summary>
        /// Devuelve el factor fijo 85 para obtener resultados reproducibles.
        /// </summary>
        public int Next() => 85;
    }
}
