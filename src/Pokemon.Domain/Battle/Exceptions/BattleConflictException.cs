namespace Pokemon.Domain.Battle.Exceptions;

/// <summary>
/// Acción incompatible con el turno, los usos o la versión actual de la partida.
/// </summary>
public sealed class BattleConflictException(string message) : Exception(message);
