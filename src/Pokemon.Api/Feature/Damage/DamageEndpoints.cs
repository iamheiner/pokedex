using Pokemon.Api.Feature.Damage.Queries;

namespace Pokemon.Api.Feature.Damage;

/// <summary>Agrupa las rutas HTTP del cálculo de daño.</summary>
public static class DamageEndpoints
{
    /// <summary>Registra la consulta de daño sin modificar el estado de los participantes.</summary>
    public static IEndpointRouteBuilder MapDamageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        CalculateDamageEndpoint.Map(endpoints);
        return endpoints;
    }
}
