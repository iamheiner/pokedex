using Pokemon.Api.Feature.Health.Queries;

namespace Pokemon.Api.Feature.Health;

/// <summary>Agrupa las sondas de actividad y disponibilidad de la API.</summary>
public static class HealthEndpoints
{
    /// <summary>Registra las sondas anónimas para el orquestador sin exponer datos de negocio.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        GetHealthEndpoint.Map(endpoints);
        GetReadinessEndpoint.Map(endpoints);
        return endpoints;
    }
}
