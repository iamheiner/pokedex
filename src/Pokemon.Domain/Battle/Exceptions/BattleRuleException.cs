namespace Pokemon.Domain.Battle.Exceptions;

/// <summary>Datos de creación que no permiten iniciar un combate válido.</summary>
public sealed class BattleRuleException(string message) : Exception(message);
