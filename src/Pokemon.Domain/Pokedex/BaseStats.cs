namespace Pokemon.Domain.Pokedex;

/// <summary>Objeto valor con las seis estadísticas base compartidas por una especie.</summary>
public sealed record BaseStats
{
    public int Health { get; }
    public int Attack { get; }
    public int Defense { get; }
    public int SpecialAttack { get; }
    public int SpecialDefense { get; }
    public int Speed { get; }

    /// <summary>Construye las seis estadísticas base y exige que cada valor esté entre 1 y 10000.</summary>
    public BaseStats(int health, int attack, int defense, int specialAttack, int specialDefense, int speed)
    {
        PokedexGuard.Require(new[] { health, attack, defense, specialAttack, specialDefense, speed }
            .All(value => value is >= 1 and <= 10000), "Stats must be between 1 and 10000.");
        Health = health; Attack = attack; Defense = defense;
        SpecialAttack = specialAttack; SpecialDefense = specialDefense; Speed = speed;
    }
}
