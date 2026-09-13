using Pokemon.Api.Feature.Damage.Queries;

namespace Pokemon.Api.Feature.Damage;

/// <summary>
/// Agrupa las rutas HTTP del cálculo de daño.
/// </summary>
public static class DamageEndpoints
{
    public static IEndpointRouteBuilder MapDamageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        CalculateDamageEndpoint.Map(endpoints);
        return endpoints;
    }
}
