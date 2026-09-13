using Pokemon.Domain.Pokedex.Exceptions;
namespace Pokemon.Domain.Pokedex;

/// <summary>
/// Una modificación de especie debe conservar la validez de los movimientos ya aprendidos.
/// </summary>
public static class LearningPolicy
{
    /// <summary>
    /// Rechaza un plan de aprendizaje que invalide movimientos ya aprendidos por los ejemplares existentes.
    /// </summary>
    public static void EnsureCompatible(Species proposed, IEnumerable<OwnedPokemon> existingPokemon)
    {
        if (existingPokemon.Any(pokemon => pokemon.MoveIds.Any(move => !proposed.CanLearn(move, pokemon.Level))))
            throw new PokedexConflictException("The new learnset would invalidate an existing Pokemon.");
    }
}
