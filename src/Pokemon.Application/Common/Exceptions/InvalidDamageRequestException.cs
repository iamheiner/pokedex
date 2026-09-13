namespace Pokemon.Application.Common.Exceptions;

/// <summary>
/// Error esperado en los datos del cálculo; se distingue de fallos internos.
/// </summary>
public sealed class InvalidDamageRequestException(string message) : Exception(message);
