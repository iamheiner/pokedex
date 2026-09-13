namespace Pokemon.Domain.Pokedex.Exceptions;

public sealed class PokedexConflictException(string message) : Exception(message);
