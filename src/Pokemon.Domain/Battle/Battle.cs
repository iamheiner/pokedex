using Pokemon.Domain.Battle.Exceptions;
namespace Pokemon.Domain.Battle;

/// <summary>
/// Raíz del agregado de partida. Protege adversarios, orden de turnos, salud, usos,
/// historial y finalización. Cada acción produce una nueva versión inmutable.
/// </summary>
public sealed class Battle
{
    public Guid Id { get; }
    public BattlePokemon First { get; }
    public BattlePokemon Second { get; }
    public IReadOnlyList<BattleTurn> Turns { get; }
    public int Version => Turns.Count + 1;
    public BattlePhase Phase => First.Snapshot.CurrentHealth == 0 || Second.Snapshot.CurrentHealth == 0
        ? BattlePhase.Finished : BattlePhase.AwaitingAction;
    public Guid? NextPokemonId { get; }
    public Guid? WinnerId => Phase != BattlePhase.Finished || IsDraw ? null
        : First.Snapshot.CurrentHealth > 0 ? First.Snapshot.Id : Second.Snapshot.Id;
    public bool IsDraw => First.Snapshot.CurrentHealth == 0 && Second.Snapshot.CurrentHealth == 0;

    /// <summary>Construye el estado inmutable de la partida y elimina el siguiente actor si el combate ha terminado.</summary>
    private Battle(Guid id, BattlePokemon first, BattlePokemon second, Guid? nextPokemonId, IEnumerable<BattleTurn> turns)
    {
        Id = id; First = first; Second = second;
        Turns = Array.AsReadOnly(turns.ToArray());
        NextPokemonId = Phase == BattlePhase.Finished ? null : nextPokemonId;
    }

    /// <summary>Exige dos ejemplares distintos con salud. En empate de velocidad empieza First.</summary>
    public static Battle Start(Guid id, BattlePokemon first, BattlePokemon second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (id == Guid.Empty || first.Snapshot.Id == second.Snapshot.Id)
            throw new BattleRuleException("A battle needs an identity and two distinct Pokemon.");
        if (first.Snapshot.CurrentHealth == 0 || second.Snapshot.CurrentHealth == 0)
            throw new BattleRuleException("Both Pokemon must have positive health to start.");
        if (first.Moves.Concat(second.Moves).Any(m => m.RemainingUses != BattleMove.InitialUses))
            throw new BattleRuleException("A new battle requires fresh move uses.");
        var next = first.Snapshot.Speed >= second.Snapshot.Speed ? first.Snapshot.Id : second.Snapshot.Id;
        return new(id, first, second, next, []);
    }

    /// <summary>
    /// Comprueba la acción antes de obtener azar. ExpectedVersion rechaza reintentos y
    /// peticiones concurrentes basadas en un estado antiguo; no se ejecutan dos veces.
    /// MoveId nulo selecciona esfuerzo únicamente cuando todos los movimientos están agotados.
    /// </summary>
    public void EnsureCanAct(Guid pokemonId, Guid? moveId, int expectedVersion)
    {
        if (Version != expectedVersion) throw new BattleConflictException("Stale battle version; read the current state before acting.");
        if (Phase == BattlePhase.Finished) throw new BattleConflictException("The battle has finished.");
        if (pokemonId != NextPokemonId) throw new BattleConflictException("This Pokemon cannot act in the current turn.");
        var actor = pokemonId == First.Snapshot.Id ? First : Second;
        if (moveId is null)
        {
            if (actor.Moves.Any(m => m.RemainingUses > 0))
                throw new BattleConflictException("Struggle is available only when all learned moves are exhausted.");
        }
        else if (!actor.Moves.Any(m => m.Id == moveId && m.RemainingUses > 0))
            throw new BattleConflictException("Select a learned move with remaining uses.");
    }

    /// <summary>
    /// Resuelve selección, daño, consumo y derrota atómicamente. Los ataques normales
    /// reutilizan DamageCalculator. Esfuerzo causa al menos un punto, ignora inmunidades
    /// y tiene retroceso, para que ni las inmunidades ni el redondeo provoquen bucles infinitos.
    /// </summary>
    public Battle PlayTurn(Guid pokemonId, Guid? moveId, int expectedVersion, int randomFactor)
    {
        EnsureCanAct(pokemonId, moveId, expectedVersion);
        if (randomFactor is < 85 or > 100) throw new ArgumentOutOfRangeException(nameof(randomFactor));
        var actorIsFirst = pokemonId == First.Snapshot.Id;
        var actor = actorIsFirst ? First : Second;
        var defender = actorIsFirst ? Second : First;
        var selected = actor.Moves.SingleOrDefault(m => m.Id == moveId);
        var isStruggle = selected is null;
        var result = isStruggle ? null : DamageCalculator.Calculate(actor.Snapshot, selected!.Definition, defender.Snapshot, randomFactor);
        var calculated = result?.Damage ?? Math.Max(1, defender.Snapshot.TotalHealth / 10);
        var applied = Math.Min(calculated, defender.Snapshot.CurrentHealth);
        var recoil = isStruggle ? Math.Min(actor.Snapshot.CurrentHealth, Math.Max(1, actor.Snapshot.TotalHealth / 10)) : 0;
        var updatedActor = actor.Apply(recoil, moveId);
        var updatedDefender = defender.Apply(applied, null);
        var turn = new BattleTurn(Version, actor.Snapshot.Id, defender.Snapshot.Id, moveId,
            selected?.Definition.Name ?? "Struggle", isStruggle, calculated, applied, recoil,
            result?.Effectiveness, isStruggle ? null : randomFactor,
            updatedActor.Snapshot.CurrentHealth, updatedDefender.Snapshot.CurrentHealth);
        return new(Id, actorIsFirst ? updatedActor : updatedDefender,
            actorIsFirst ? updatedDefender : updatedActor, defender.Snapshot.Id, Turns.Append(turn));
    }
}
