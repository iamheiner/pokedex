namespace Pokemon.Domain.Pokedex;

/// <summary>
/// Raíz de agregado de un ejemplar propio. Guarda referencias por identidad a su especie
/// y a exactamente cuatro movimientos aprendidos; dos ejemplares pueden compartir especie.
/// </summary>
public sealed class OwnedPokemon
{
    public Guid Id { get; }
    public Guid SpeciesId { get; }
    public string Name { get; }
    public int Level { get; }
    public int CurrentHealth { get; }
    public int TotalHealth { get; }
    public IReadOnlyList<Guid> MoveIds { get; }

    /// <summary>
    /// Construye un ejemplar validando su salud, nivel y cuatro movimientos compatibles con la especie.
    /// </summary>
    public OwnedPokemon(Guid id, Species species, string name, int level, int currentHealth, int totalHealth, IEnumerable<Guid> moveIds)
    {
        PokedexGuard.Identity(id);
        PokedexGuard.Require(species is not null && moveIds is not null, "Species and moves are required.");
        PokedexGuard.Require(level is >= 1 and <= 100, "Level must be between 1 and 100.");
        PokedexGuard.Require(totalHealth is >= 1 and <= 10000 && currentHealth >= 0 && currentHealth <= totalHealth,
            "Health must satisfy 0 <= current <= total <= 10000, with a positive total.");
        var selected = moveIds!.ToArray();
        PokedexGuard.Require(selected.Length == 4 && selected.Distinct().Count() == 4,
            "Exactly four distinct learned moves are required.");
        PokedexGuard.Require(selected.All(id => species!.CanLearn(id, level)), "A selected move cannot be learned by this species at this level.");
        Id = id; SpeciesId = species!.Id; Name = PokedexGuard.Name(name); Level = level;
        CurrentHealth = currentHealth; TotalHealth = totalHealth; MoveIds = Array.AsReadOnly(selected);
    }
}
