using Pokemon.Domain;

namespace Pokemon.Api.Feature.Damage.Contracts;

// Este contrato pertenece al cálculo de daño; otras funcionalidades pueden tener
// entradas distintas sin modificar la entidad de dominio ni este endpoint.
public sealed record CombatantRequest(Guid Id, string Name, int Level, PokemonType Type,
    int CurrentHealth, int TotalHealth, int Attack, int Defense, int SpecialAttack,
    int SpecialDefense, int Speed, MoveRequest[] Moves)
{
    public Combatant ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Moves);
        if (Moves.Any(m => m is null)) throw new ArgumentException("Moves cannot contain null entries.");
        return new Combatant(id: Id, name: Name, level: Level, type: Type,
            currentHealth: CurrentHealth, totalHealth: TotalHealth, attack: Attack, defense: Defense,
            specialAttack: SpecialAttack, specialDefense: SpecialDefense, speed: Speed,
            moves: Moves.Select(m => new Move(m.Name, m.Power, m.Type)));
    }
}
