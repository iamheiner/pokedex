namespace Pokemon.Domain.Pokedex;

/// <summary>Relación de aprendizaje: qué movimiento puede aprender una especie y desde qué nivel.</summary>
public sealed record LearnableMove
{
    public Guid MoveId { get; }
    public int Level { get; }
    public LearnableMove(Guid moveId, int level)
    {
        PokedexGuard.Identity(moveId);
        PokedexGuard.Require(level is >= 1 and <= 100, "Learning level must be between 1 and 100.");
        MoveId = moveId; Level = level;
    }
}
