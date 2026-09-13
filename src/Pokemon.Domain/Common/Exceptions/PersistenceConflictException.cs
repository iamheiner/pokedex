namespace Pokemon.Domain.Common.Exceptions;

/// <summary>
/// Una operación atómica entra en conflicto con una identidad o referencia ya existente.
/// </summary>
public sealed class PersistenceConflictException(string message) : Exception(message);
