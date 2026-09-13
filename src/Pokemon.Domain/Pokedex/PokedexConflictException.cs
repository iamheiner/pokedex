namespace Pokemon.Domain.Pokedex;
public sealed class PokedexConflictException(string message) : Exception(message);
