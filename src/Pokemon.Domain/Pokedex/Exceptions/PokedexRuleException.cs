namespace Pokemon.Domain.Pokedex.Exceptions;

/// <summary>Incumplimiento de una regla de la Pokédex; no representa un fallo técnico.</summary>
public sealed class PokedexRuleException(string message) : Exception(message);
