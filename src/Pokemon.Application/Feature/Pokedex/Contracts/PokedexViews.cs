using Pokemon.Domain;
namespace Pokemon.Application.Feature.Pokedex.Contracts;

public sealed record StatsView(int Health, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed);
public sealed record MoveView(Guid Id, string Name, int Power, PokemonType Type);
public sealed record LearningView(MoveView Move, int Level);
public sealed record SpeciesView(Guid Id, string Name, PokemonType Type, StatsView Stats, IReadOnlyList<LearningView> Learnset);
public sealed record PokemonView(Guid Id, Guid SpeciesId, string SpeciesName, string Name, PokemonType Type,
    int Level, int CurrentHealth, int TotalHealth, StatsView Stats, IReadOnlyList<MoveView> Moves);
public sealed record PossibleMovesView(Guid PokemonId, Guid SpeciesId, IReadOnlyList<LearningView> Moves);
