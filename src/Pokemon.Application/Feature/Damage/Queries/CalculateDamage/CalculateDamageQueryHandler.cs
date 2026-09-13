using Pokemon.Application.Common.Exceptions;
using MediatR;
using Pokemon.Domain;

namespace Pokemon.Application.Feature.Damage.Queries.CalculateDamage;

/// <summary>
/// Atiende la Query de daño: selecciona un movimiento aprendido, obtiene el factor
/// aleatorio y delega la fórmula en el dominio. No modifica salud ni guarda resultados.
/// </summary>
/// <remarks>
/// En CQRS el handler ejecuta el caso de uso descrito por la petición.
/// Application coordina los pasos; DamageCalculator conserva las reglas de negocio.
/// La API no instancia este handler: ISender.Send permite que MediatR lo resuelva.
/// </remarks>
/// <param name="random">
/// Proveedor de un porcentaje entre 85 y 100. Se inyecta mediante el constructor
/// para usar aleatoriedad real en la API y valores fijos en las pruebas.
/// </param>
public sealed class CalculateDamageQueryHandler(IDamageRandom random)
    : IRequestHandler<CalculateDamageQuery, DamageResult>
{
    /// <summary>
    /// Procesa la petición y devuelve el daño, la efectividad y el factor utilizado.
    /// </summary>
    /// <param name="request">Atacante, nombre del movimiento y defensor del cálculo.</param>
    /// <param name="cancellationToken">Permite cancelar antes de iniciar el cálculo.</param>
    /// <exception cref="InvalidDamageRequestException">Falta el nombre o el movimiento no está aprendido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">El factor aleatorio está fuera de 85–100.</exception>
    /// <exception cref="OperationCanceledException">La petición ya estaba cancelada.</exception>
    public Task<DamageResult> Handle(CalculateDamageQuery request, CancellationToken cancellationToken)
    {
        // La API transmite la cancelación del cliente a través de MediatR.
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Defender);
        if (string.IsNullOrWhiteSpace(request.MoveName))
            throw new InvalidDamageRequestException("Move name is required.");

        // Se busca entre los movimientos de ESTE ejemplar, no en un catálogo general.
        // Combatant impide nombres duplicados. La comparación ignora mayúsculas.
        // SingleOrDefault devuelve null si no hay coincidencia; ?? lanza el error.
        var move = request.Attacker.Moves.SingleOrDefault(m =>
            string.Equals(m.Name, request.MoveName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDamageRequestException("The attacker has not learned this move.");

        // Una sola muestra aleatoria por cálculo. El dominio valida su intervalo,
        // aplica la efectividad del tipo del movimiento y redondea el daño hacia abajo.
        var result = DamageCalculator.Calculate(request.Attacker, move, request.Defender, random.Next());

        // MediatR utiliza Task como contrato. El cálculo es inmediato, no realiza E/S
        // y no necesita crear un hilo con Task.Run ni introducir un await artificial.
        return Task.FromResult(result);
    }
}
