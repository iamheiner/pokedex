namespace Pokemon.Domain.Pokedex;

/// <summary>
/// Raíz de agregado con identidad estable para editar un movimiento del catálogo.
/// Move sigue siendo el objeto valor utilizado por la calculadora del ejercicio 1.
/// </summary>
public sealed class CatalogMove
{
    public Guid Id { get; }
    public string Name { get; }
    public int Power { get; }
    public PokemonType Type { get; }
    /// <summary>
    /// Construye un movimiento del catálogo validando identidad, nombre, potencia y tipo.
    /// </summary>
    public CatalogMove(Guid id, string name, int power, PokemonType type)
    {
        PokedexGuard.Identity(id);
        PokedexGuard.Require(power is >= 1 and <= 250, "Power must be between 1 and 250.");
        PokedexGuard.Require(Enum.IsDefined(type), "Unknown move type.");
        Id = id; Name = PokedexGuard.Name(name); Power = power; Type = type;
    }
    /// <summary>
    /// Convierte el movimiento del catálogo al objeto valor utilizado por la calculadora de daño.
    /// </summary>
    public Move ToDamageMove() => new(Name, Power, Type);
}
