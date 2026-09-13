using Pokemon.Domain;

namespace Pokemon.Api.Feature.Damage.Contracts;

public sealed record MoveRequest(string Name, int Power, PokemonType Type);
