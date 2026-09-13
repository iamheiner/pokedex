namespace Pokemon.Api.Feature.Damage.Contracts;

/// <summary>
/// Datos HTTP para calcular el daño de un movimiento conocido por el atacante.
/// </summary>
public sealed record DamageRequest(CombatantRequest Attacker, string MoveName, CombatantRequest Defender);
