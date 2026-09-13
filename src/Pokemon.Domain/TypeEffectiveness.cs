namespace Pokemon.Domain;

/// <summary>
/// Relaciona explícitamente el tipo del movimiento con el tipo del defensor.
/// </summary>
public static class TypeEffectiveness
{
    // Cada fila declara debilidades, resistencias e inmunidades del defensor.
    // Los cruces no enumerados son neutros (×1). El orden del enum no interviene.
    private sealed record Matchups(PokemonType[] Strong, PokemonType[] Weak, PokemonType[] Immune);
    private static readonly IReadOnlyDictionary<PokemonType, Matchups> Rows =
        new Dictionary<PokemonType, Matchups>
    {
        [PokemonType.Normal] = new([], [PokemonType.Rock, PokemonType.Steel], [PokemonType.Ghost]),
        [PokemonType.Fire] = new([PokemonType.Grass, PokemonType.Ice, PokemonType.Bug, PokemonType.Steel], [PokemonType.Fire, PokemonType.Water, PokemonType.Rock, PokemonType.Dragon], []),
        [PokemonType.Water] = new([PokemonType.Fire, PokemonType.Ground, PokemonType.Rock], [PokemonType.Water, PokemonType.Grass, PokemonType.Dragon], []),
        [PokemonType.Electric] = new([PokemonType.Water, PokemonType.Flying], [PokemonType.Electric, PokemonType.Grass, PokemonType.Dragon], [PokemonType.Ground]),
        [PokemonType.Grass] = new([PokemonType.Water, PokemonType.Ground, PokemonType.Rock], [PokemonType.Fire, PokemonType.Grass, PokemonType.Poison, PokemonType.Flying, PokemonType.Bug, PokemonType.Dragon, PokemonType.Steel], []),
        [PokemonType.Ice] = new([PokemonType.Grass, PokemonType.Ground, PokemonType.Flying, PokemonType.Dragon], [PokemonType.Fire, PokemonType.Water, PokemonType.Ice, PokemonType.Steel], []),
        [PokemonType.Fighting] = new([PokemonType.Normal, PokemonType.Ice, PokemonType.Rock, PokemonType.Dark, PokemonType.Steel], [PokemonType.Poison, PokemonType.Flying, PokemonType.Psychic, PokemonType.Bug, PokemonType.Fairy], [PokemonType.Ghost]),
        [PokemonType.Poison] = new([PokemonType.Grass, PokemonType.Fairy], [PokemonType.Poison, PokemonType.Ground, PokemonType.Rock, PokemonType.Ghost], [PokemonType.Steel]),
        [PokemonType.Ground] = new([PokemonType.Fire, PokemonType.Electric, PokemonType.Poison, PokemonType.Rock, PokemonType.Steel], [PokemonType.Grass, PokemonType.Bug], [PokemonType.Flying]),
        [PokemonType.Flying] = new([PokemonType.Grass, PokemonType.Fighting, PokemonType.Bug], [PokemonType.Electric, PokemonType.Rock, PokemonType.Steel], []),
        [PokemonType.Psychic] = new([PokemonType.Fighting, PokemonType.Poison], [PokemonType.Psychic, PokemonType.Steel], [PokemonType.Dark]),
        [PokemonType.Bug] = new([PokemonType.Grass, PokemonType.Psychic, PokemonType.Dark], [PokemonType.Fire, PokemonType.Fighting, PokemonType.Poison, PokemonType.Flying, PokemonType.Ghost, PokemonType.Steel, PokemonType.Fairy], []),
        [PokemonType.Rock] = new([PokemonType.Fire, PokemonType.Ice, PokemonType.Flying, PokemonType.Bug], [PokemonType.Fighting, PokemonType.Ground, PokemonType.Steel], []),
        [PokemonType.Ghost] = new([PokemonType.Psychic, PokemonType.Ghost], [PokemonType.Dark], [PokemonType.Normal]),
        [PokemonType.Dragon] = new([PokemonType.Dragon], [PokemonType.Steel], [PokemonType.Fairy]),
        [PokemonType.Dark] = new([PokemonType.Psychic, PokemonType.Ghost], [PokemonType.Fighting, PokemonType.Dark, PokemonType.Fairy], []),
        [PokemonType.Steel] = new([PokemonType.Ice, PokemonType.Rock, PokemonType.Fairy], [PokemonType.Fire, PokemonType.Water, PokemonType.Electric, PokemonType.Steel], []),
        [PokemonType.Fairy] = new([PokemonType.Fighting, PokemonType.Dragon, PokemonType.Dark], [PokemonType.Fire, PokemonType.Poison, PokemonType.Steel], []),
    };

    /// <summary>
    /// Valida al inicializar que la tabla cubra todos los tipos sin combinaciones contradictorias.
    /// </summary>
    static TypeEffectiveness()
    {
        // Si se amplía el enum, una fila ausente o contradictoria falla explícitamente.
        if (!Enum.GetValues<PokemonType>().All(Rows.ContainsKey))
            throw new InvalidOperationException("Every attack type must have a matchup row.");
        foreach (var row in Rows.Values)
        {
            var defenders = row.Strong.Concat(row.Weak).Concat(row.Immune).ToArray();
            if (defenders.Any(type => !Enum.IsDefined(type)) || defenders.Distinct().Count() != defenders.Length)
                throw new InvalidOperationException("Invalid or contradictory type matchup.");
        }
    }

    /// <summary>
    /// Devuelve 0, 0,5, 1 o 2; compara movimiento y defensor.
    /// </summary>
    public static decimal Against(PokemonType moveType, PokemonType defenderType)
    {
        if (!Enum.IsDefined(moveType) || !Enum.IsDefined(defenderType))
            throw new ArgumentException("Unknown Pokemon type.");
        var row = Rows[moveType];
        if (row.Immune.Contains(defenderType)) return 0m;
        if (row.Strong.Contains(defenderType)) return 2m;
        if (row.Weak.Contains(defenderType)) return 0.5m;
        return 1m;
    }
}
