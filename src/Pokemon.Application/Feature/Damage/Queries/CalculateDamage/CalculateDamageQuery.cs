using MediatR;
using Pokemon.Domain;

namespace Pokemon.Application.Feature.Damage.Queries.CalculateDamage;

/// <summary>
/// Petición de cálculo de daño: contiene los datos de entrada, sin ejecutar la fórmula.
/// En CQRS una Query obtiene un resultado sin modificar el estado de negocio.
/// Aunque el resultado varía por el factor aleatorio, no cambia la salud de los Pokémon.
/// </summary>
/// <param name="Attacker">Pokémon que ataca, con sus movimientos aprendidos.</param>
/// <param name="MoveName">Nombre del movimiento que se quiere utilizar.</param>
/// <param name="Defender">Pokémon que recibiría el daño.</param>
/// <remarks>
/// IRequest indica a MediatR que esta petición espera un DamageResult.
/// MediatR la enviará al handler registrado para este tipo de Query.
/// </remarks>
public sealed record CalculateDamageQuery(Combatant Attacker, string MoveName, Combatant Defender)
    : IRequest<DamageResult>;
