namespace Pokemon.Domain;

public sealed record DamageResult(int Damage, decimal Effectiveness, int RandomFactor);

public static class DamageCalculator
{
    /// <summary>
    /// Calcula cuántos puntos de salud quitaría un movimiento (ataque) al defensor.
    /// Solo devuelve el daño: no descuenta salud ni avanza un turno de combate.
    /// </summary>
    /// <param name="attacker">Pokémon que ataca: aporta su nivel y su fuerza de ataque.</param>
    /// <param name="move">Movimiento utilizado: aporta su poder y su tipo, por ejemplo fuego.</param>
    /// <param name="defender">Pokémon que recibe el ataque: aporta su defensa y su tipo.</param>
    /// <param name="randomFactor">
    /// Porcentaje entero entre 85 y 100. Lo proporciona quien llama al método;
    /// así las pruebas pueden fijarlo y obtener siempre el mismo resultado.
    /// </param>
    /// <returns>Daño entero, multiplicador de efectividad y porcentaje aleatorio utilizado.</returns>
    public static DamageResult Calculate(Combatant attacker, Move move, Combatant defender, int randomFactor)
    {
        // Los tres objetos son necesarios para consultar las características del ataque.
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(defender);

        // Un Pokémon solo puede utilizar uno de los movimientos que tiene aprendidos.
        if (!attacker.Moves.Contains(move)) throw new ArgumentException("The attacker has not learned this move.");

        // 85 equivale al 85 % del daño y 100 al 100 %: la variación máxima es del 15 %.
        if (randomFactor is < 85 or > 100) throw new ArgumentOutOfRangeException(nameof(randomFactor));

        // Se compara el tipo DEL MOVIMIENTO con el del defensor, no los dos Pokémon.
        // La tabla devuelve 0 (inmunidad), 0,5 (mitad), 1 (normal) o 2 (doble).
        // Ejemplo: un movimiento de fuego contra un defensor de planta multiplica por 2.
        var effectiveness = TypeEffectiveness.Against(move.Type, defender.Type);

        // Fórmula del enunciado:
        // 1. (2 × nivel / 5 + 2) incorpora el nivel del atacante.
        // 2. Se multiplica por su ataque y por el poder del movimiento.
        // 3. Se divide por la defensa del rival y por 50 (constante de la fórmula).
        // 4. Se aplican la efectividad y el porcentaje aleatorio.
        // Se usan ataque y defensa normales; las estadísticas especiales quedan fuera
        // del alcance acordado. El modelo garantiza que la defensa sea mayor que cero.
        // El sufijo «m» indica decimal en C#: conserva las fracciones durante el cálculo.
        var damage = ((2m * attacker.Level / 5m + 2m) * attacker.Attack * move.Power / defender.Defense / 50m)
            * effectiveness * randomFactor / 100m;

        // Solo se redondea al final y hacia abajo: 20,9 pasa a 20 puntos de daño.
        // No se fuerza un mínimo de 1: una inmunidad debe seguir produciendo cero.
        // Se incluyen los factores utilizados para poder explicar el resultado.
        return new DamageResult((int)decimal.Floor(damage), effectiveness, randomFactor);
    }
}
