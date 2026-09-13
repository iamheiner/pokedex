using Pokemon.Domain.Pokedex.Exceptions;
namespace Pokemon.Domain.Pokedex;

internal static class PokedexGuard
{
    /// <summary>Lanza una excepción de regla de Pokédex cuando la condición indicada no se cumple.</summary>
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new PokedexRuleException(message);
    }
    /// <summary>Valida la longitud del nombre y devuelve su valor sin espacios en los extremos.</summary>
    public static string Name(string value)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100, "Name must contain 1 to 100 characters.");
        return value.Trim();
    }
    /// <summary>Rechaza un identificador vacío para las entidades de Pokédex.</summary>
    public static void Identity(Guid id) => Require(id != Guid.Empty, "Identity is required.");
}
