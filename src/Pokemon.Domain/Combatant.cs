namespace Pokemon.Domain;

/// <summary>
/// Representa un ejemplar de Pokémon que puede intervenir como atacante o defensor.
/// Reúne sus características y los movimientos (ataques) que tiene aprendidos.
/// </summary>
/// <remarks>
/// En DDD es la raíz del agregado: controla sus datos y su colección de movimientos
/// para que siempre cumplan las reglas del modelo, también llamadas invariantes.
/// Su Id distingue dos ejemplares aunque tengan el mismo nombre y características.
/// En esta primera parte sus propiedades son de solo lectura: representa los datos
/// necesarios para calcular daño, sin gestionar todavía la evolución de un combate.
/// </remarks>
public sealed class Combatant
{
    /// <summary>Identificador del ejemplar; no es el identificador de una especie.</summary>
    public Guid Id { get; }

    /// <summary>Nombre del Pokémon, por ejemplo «Charmander».</summary>
    public string Name { get; }

    /// <summary>Grado de desarrollo, entre 1 y 100. Interviene en la fórmula de daño.</summary>
    public int Level { get; }

    /// <summary>
    /// Categoría del Pokémon, por ejemplo fuego o planta. Cuando recibe un ataque,
    /// se compara con el tipo del movimiento recibido para obtener la efectividad.
    /// Esta solución simplificada admite un único tipo por Pokémon.
    /// </summary>
    public PokemonType Type { get; }

    /// <summary>
    /// Puntos de salud que le quedan. Con cero estaría fuera de combate, aunque
    /// la calculadora permite calcular daño teórico independientemente de su salud.
    /// </summary>
    public int CurrentHealth { get; }

    /// <summary>Salud máxima: un Pokémon con 30 de 50 puntos tiene 30 actuales y 50 totales.</summary>
    public int TotalHealth { get; }

    /// <summary>Fuerza del Pokémon que se combina con el poder del movimiento para atacar.</summary>
    public int Attack { get; }

    /// <summary>
    /// Resistencia frente al ataque: cuanto mayor es, menor resulta el daño recibido.
    /// Debe ser positiva porque la fórmula divide entre este valor.
    /// </summary>
    public int Defense { get; }

    /// <summary>
    /// Fuerza para movimientos de categoría especial. Se conserva por el enunciado,
    /// pero la fórmula simplificada acordada utiliza siempre Attack.
    /// </summary>
    public int SpecialAttack { get; }

    /// <summary>
    /// Resistencia frente a movimientos especiales. Se conserva por el enunciado;
    /// el cálculo actual utiliza siempre Defense.
    /// </summary>
    public int SpecialDefense { get; }

    /// <summary>
    /// Rapidez del Pokémon, que puede servir para decidir quién actúa primero
    /// al modelar un combate. No interviene en este cálculo de daño.
    /// </summary>
    public int Speed { get; }

    /// <summary>
    /// Movimientos que conoce este ejemplar, como máximo cuatro con nombres distintos.
    /// No es la lista de todos los movimientos que podría llegar a aprender.
    /// </summary>
    public IReadOnlyList<Move> Moves { get; }

    /// <summary>
    /// Crea un ejemplar válido. Si algún dato incumple las reglas, lanza una excepción
    /// y evita construir un Pokémon con un estado incoherente.
    /// </summary>
    public Combatant(Guid id, string name, int level, PokemonType type,
        int currentHealth, int totalHealth, int attack, int defense,
        int specialAttack, int specialDefense, int speed, IEnumerable<Move> moves)
    {
        // La identidad distingue al ejemplar y el nombre permite reconocerlo.
        if (id == Guid.Empty) throw new ArgumentException("Identity is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Se limita el nivel y se rechazan valores numéricos que no sean un tipo definido.
        if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(level));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));

        // Las estadísticas son positivas. El máximo 10000 es un límite elegido para
        // esta implementación, no un límite oficial del juego. La salud actual se
        // comprueba aparte porque sí puede ser cero.
        foreach (var stat in new[] { totalHealth, attack, defense, specialAttack, specialDefense, speed })
            if (stat is < 1 or > 10000) throw new ArgumentException("Stats must be between 1 and 10000.");

        // No puede tener salud negativa ni más puntos que su capacidad total.
        if (currentHealth < 0 || currentHealth > totalHealth)
            throw new ArgumentOutOfRangeException(nameof(currentHealth));

        // La colección debe existir, pero puede estar vacía: un defensor no necesita
        // movimientos para que podamos calcular cuánto daño recibiría.
        ArgumentNullException.ThrowIfNull(moves);

        // Se hace una copia para que quien proporcionó la colección no pueda modificar
        // después los movimientos del Pokémon cambiando su lista o array original.
        var learned = moves.ToArray();

        // Se rechazan más de cuatro movimientos, elementos nulos y nombres repetidos.
        // La comparación ignora mayúsculas: «Ascuas» y «ascuas» cuentan como el mismo nombre.
        if (learned.Length > 4 || learned.Any(m => m is null) ||
            learned.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != learned.Length)
            throw new ArgumentException("At most four distinct moves are allowed.", nameof(moves));

        // Solo se guardan los datos cuando todas las validaciones han terminado.
        Id = id;
        Name = name;
        Level = level;
        Type = type;
        CurrentHealth = currentHealth;
        TotalHealth = totalHealth;
        Attack = attack;
        Defense = defense;
        SpecialAttack = specialAttack;
        SpecialDefense = specialDefense;
        Speed = speed;

        // La copia se expone como colección de solo lectura para proteger sus reglas.
        Moves = Array.AsReadOnly(learned);
    }
}
