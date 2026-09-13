namespace Pokemon.Application.Feature.Battle;
public sealed class BattleNotFoundException(string message) : Exception(message);
