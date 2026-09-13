namespace Pokemon.Domain.Pokedex;

/// <summary>
/// Raíz de agregado del Pokémon base: identidad, tipo, estadísticas y plan de aprendizaje.
/// No contiene salud actual ni movimientos elegidos de un ejemplar concreto.
/// </summary>
public sealed class Species
{
    public Guid Id { get; }
    public string Name { get; }
    public PokemonType Type { get; }
    public BaseStats Stats { get; }
    public IReadOnlyList<LearnableMove> Learnset { get; }

    /// <summary>Construye una especie validando sus estadísticas y un plan de aprendizaje sin movimientos duplicados.</summary>
    public Species(Guid id, string name, PokemonType type, BaseStats stats, IEnumerable<LearnableMove> learnset)
    {
        PokedexGuard.Identity(id);
        PokedexGuard.Require(Enum.IsDefined(type), "Unknown Pokemon type.");
        PokedexGuard.Require(stats is not null && learnset is not null, "Stats and learnset are required.");
        var entries = learnset!.ToArray();
        PokedexGuard.Require(entries.All(e => e is not null) && entries.Select(e => e.MoveId).Distinct().Count() == entries.Length,
            "Learnset must contain distinct, non-null moves.");
        Id = id; Name = PokedexGuard.Name(name); Type = type; Stats = stats!;
        Learnset = Array.AsReadOnly(entries);
    }

    /// <summary>El permiso depende de la especie y el nivel, nunca de coincidir en tipo.</summary>
    public bool CanLearn(Guid moveId, int level) => Learnset.Any(e => e.MoveId == moveId && e.Level <= level);
}
