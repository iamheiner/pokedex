namespace Pokemon.Domain.Battle;

/// <summary>
/// Copia de un movimiento al iniciar la partida, con usos propios del combate.
/// Cinco usos por movimiento es una regla simplificada de esta solución.
/// </summary>
public sealed record BattleMove
{
    public const int InitialUses = 5;
    public Guid Id { get; }
    public Move Definition { get; }
    public int RemainingUses { get; }
    public BattleMove(Guid id, Move definition) : this(id, definition, InitialUses) { }
    private BattleMove(Guid id, Move definition, int remainingUses)
    {
        if (id == Guid.Empty) throw new BattleRuleException("Move identity is required.");
        ArgumentNullException.ThrowIfNull(definition);
        Id = id; Definition = definition; RemainingUses = remainingUses;
    }
    internal BattleMove Spend() => RemainingUses > 0 ? new(Id, Definition, RemainingUses - 1)
        : throw new BattleConflictException("The move has no remaining uses.");
}
