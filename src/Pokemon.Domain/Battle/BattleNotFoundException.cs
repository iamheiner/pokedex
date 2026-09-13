namespace Pokemon.Domain.Battle;
public sealed class BattleNotFoundException(string message) : Exception(message);
