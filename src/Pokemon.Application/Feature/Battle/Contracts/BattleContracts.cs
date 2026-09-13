using Pokemon.Domain;
using Pokemon.Domain.Battle;
namespace Pokemon.Application.Feature.Battle.Contracts;

public sealed record CreateBattleInput(Guid FirstPokemonId, Guid SecondPokemonId);
/// <summary>MoveId nulo solicita esfuerzo. ExpectedVersion es la versión leída de la partida.</summary>
public sealed record PlayTurnInput(Guid PokemonId, Guid? MoveId, int ExpectedVersion);
public sealed record BattleMoveView(Guid Id, string Name, PokemonType Type, int Power, int RemainingUses);
public sealed record BattlePokemonView(Guid Id, string Name, int Level, PokemonType Type, int CurrentHealth,
    int TotalHealth, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed,
    bool CanStruggle, IReadOnlyList<BattleMoveView> Moves);
public sealed record BattleView(Guid Id, int Version, BattlePhase Phase, Guid? NextPokemonId, Guid? WinnerId,
    bool IsDraw, BattlePokemonView First, BattlePokemonView Second, IReadOnlyList<BattleTurn> Turns);
