using Pokemon.Domain.Pokedex.Exceptions;
namespace Pokemon.Domain.Pokedex;

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
