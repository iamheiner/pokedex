namespace Pokemon.Application.Feature.Pokedex.Exceptions;

public sealed class PokedexNotFoundException(string message) : Exception(message);
