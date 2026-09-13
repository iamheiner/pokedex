namespace Pokemon.Domain.Pokedex;

/// <summary>Incumplimiento de una regla de la Pokédex; no representa un fallo técnico.</summary>
public sealed class PokedexRuleException(string message) : Exception(message);

internal static class PokedexGuard
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new PokedexRuleException(message);
    }
    public static string Name(string value)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100, "Name must contain 1 to 100 characters.");
        return value.Trim();
    }
    public static void Identity(Guid id) => Require(id != Guid.Empty, "Identity is required.");
}
