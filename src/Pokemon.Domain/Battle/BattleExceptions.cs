namespace Pokemon.Domain.Battle;

/// <summary>Datos de creación que no permiten iniciar un combate válido.</summary>
public sealed class BattleRuleException(string message) : Exception(message);
/// <summary>Acción incompatible con el turno, los usos o la versión actual de la partida.</summary>
public sealed class BattleConflictException(string message) : Exception(message);
