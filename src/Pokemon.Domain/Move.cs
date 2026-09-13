namespace Pokemon.Domain;

/// <summary>
/// Representa un movimiento: una acción de ataque que un Pokémon puede aprender.
/// En este ejercicio solo modelamos movimientos que causan daño.
/// </summary>
/// <remarks>
/// Es un objeto valor de DDD: no tiene identidad propia; dos movimientos son iguales
/// si coinciden su nombre, poder y tipo. «record» proporciona esa comparación por valor.
/// Las propiedades solo tienen lectura para que, una vez creado, el movimiento no cambie.
/// «sealed» impide crear clases derivadas que alteren este modelo.
/// </remarks>
public sealed record Move
{
    /// <summary>Nombre del ataque, por ejemplo «Ascuas».</summary>
    public string Name { get; }

    /// <summary>
    /// Fuerza propia del movimiento. Se combina con el ataque del Pokémon en la fórmula;
    /// no equivale directamente a los puntos de salud que perderá el defensor.
    /// </summary>
    public int Power { get; }

    /// <summary>
    /// Categoría del movimiento, por ejemplo fuego o agua. Puede ser distinta del tipo
    /// del Pokémon que lo utiliza. Se compara con el tipo del defensor para calcular
    /// si el daño es normal, doble, la mitad o nulo.
    /// </summary>
    public PokemonType Type { get; }

    /// <summary>Crea un movimiento válido; rechaza los datos que incumplen sus reglas.</summary>
    /// <param name="name">Nombre obligatorio, no vacío ni compuesto solo por espacios.</param>
    /// <param name="power">Poder entre 1 y 250, según el límite elegido para esta solución.</param>
    /// <param name="type">Uno de los tipos definidos en <see cref="PokemonType"/>.</param>
    public Move(string name, int power, PokemonType type)
    {
        // Un movimiento necesita un nombre para poder seleccionarlo entre los aprendidos.
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Este rango es una decisión de nuestra implementación, no un límite oficial
        // del juego. Excluye movimientos sin poder, como los que solo cambian estados.
        if (power is < 1 or > 250) throw new ArgumentOutOfRangeException(nameof(power));

        // C# permite convertir un número a un enum aunque no corresponda a ningún
        // elemento declarado. Esta comprobación evita introducir un tipo desconocido.
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));

        // Solo se asignan las propiedades después de validar todos los datos.
        Name = name;
        Power = power;
        Type = type;
    }
}
