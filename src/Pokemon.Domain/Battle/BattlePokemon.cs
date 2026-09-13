using Pokemon.Domain.Battle.Exceptions;
namespace Pokemon.Domain.Battle;

/// <summary>
/// Entidad interna de la partida: instantánea del ejemplar y sus movimientos.
/// Su salud y usos solo evolucionan mediante el agregado Battle, sin editar la Pokédex.
/// </summary>
public sealed class BattlePokemon
{
    public Combatant Snapshot { get; }
    public IReadOnlyList<BattleMove> Moves { get; }
    public BattlePokemon(Combatant snapshot, IEnumerable<BattleMove> moves)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(moves);
        var copy = moves.ToArray();
        if (copy.Length != 4 || copy.Any(m => m is null) || copy.Select(m => m.Id).Distinct().Count() != 4 ||
            snapshot.Moves.Count != 4 || !copy.Select(m => m.Definition).ToHashSet().SetEquals(snapshot.Moves))
            throw new BattleRuleException("A battle participant needs four distinct learned moves matching its snapshot.");
        Snapshot = snapshot; Moves = Array.AsReadOnly(copy);
    }

    internal BattlePokemon Apply(int damage, Guid? spentMove)
    {
        var p = Snapshot;
        var updated = new Combatant(id: p.Id, name: p.Name, level: p.Level, type: p.Type,
            currentHealth: Math.Max(0, p.CurrentHealth - damage), totalHealth: p.TotalHealth,
            attack: p.Attack, defense: p.Defense, specialAttack: p.SpecialAttack,
            specialDefense: p.SpecialDefense, speed: p.Speed, moves: p.Moves);
        return new(updated, Moves.Select(m => m.Id == spentMove ? m.Spend() : m));
    }
}
