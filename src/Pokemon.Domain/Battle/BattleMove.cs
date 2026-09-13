using Pokemon.Domain.Battle.Exceptions;
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
    /// <summary>Crea un movimiento de combate con todos sus usos iniciales disponibles.</summary>
    public BattleMove(Guid id, Move definition) : this(id, definition, InitialUses) { }
    /// <summary>Construye un movimiento de combate con los usos indicados y valida su identidad y definición.</summary>
    private BattleMove(Guid id, Move definition, int remainingUses)
    {
        if (id == Guid.Empty) throw new BattleRuleException("Move identity is required.");
        ArgumentNullException.ThrowIfNull(definition);
        Id = id; Definition = definition; RemainingUses = remainingUses;
    }
    /// <summary>Devuelve una nueva versión del movimiento con un uso menos o rechaza su agotamiento.</summary>
    internal BattleMove Spend() => RemainingUses > 0 ? new(Id, Definition, RemainingUses - 1)
        : throw new BattleConflictException("The move has no remaining uses.");
}
