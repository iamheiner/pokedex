namespace Pokemon.Application.Feature.Pokedex;

public sealed class PokedexNotFoundException(string message) : Exception(message);
