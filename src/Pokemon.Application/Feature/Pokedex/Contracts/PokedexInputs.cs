using Pokemon.Domain;
using Pokemon.Domain.Pokedex;
namespace Pokemon.Application.Feature.Pokedex.Contracts;

/// <summary>Datos del caso de uso; los constructores del dominio protegen las invariantes.</summary>
public sealed record StatsInput(int Health, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed)
{
    public BaseStats ToDomain() => new(Health, Attack, Defense, SpecialAttack, SpecialDefense, Speed);
}
public sealed record LearningInput(Guid MoveId, int Level);
public sealed record SpeciesInput(string Name, PokemonType Type, StatsInput Stats, LearningInput[] Learnset);
public sealed record MoveInput(string Name, int Power, PokemonType Type);
public sealed record PokemonInput(Guid SpeciesId, string Name, int Level, int CurrentHealth, int TotalHealth, Guid[] MoveIds);
