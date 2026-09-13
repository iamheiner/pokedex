namespace Pokemon.Domain.Battle;

public enum BattlePhase { AwaitingAction, Finished }

/// <summary>
/// Resultado inmutable de una acción resuelta. CalculatedDamage conserva el valor de la
/// fórmula; AppliedDamage se limita a la salud restante. El esfuerzo no usa efectividad ni azar.
/// </summary>
public sealed record BattleTurn(int Number, Guid AttackerId, Guid DefenderId, Guid? MoveId,
    string MoveName, bool IsStruggle, int CalculatedDamage, int AppliedDamage, int RecoilDamage,
    decimal? Effectiveness, int? RandomFactor, int AttackerHealth, int DefenderHealth);
