namespace Pokemon.Domain.Battle.Exceptions;

public sealed class BattleNotFoundException(string message) : Exception(message);
