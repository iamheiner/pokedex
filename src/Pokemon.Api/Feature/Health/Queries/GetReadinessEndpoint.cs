namespace Pokemon.Api.Feature.Health.Queries;

/// <summary>Comprueba mediante las sondas registradas si la API está preparada para atender peticiones.</summary>
internal static class GetReadinessEndpoint
{
    /// <summary>Registra la sonda anónima de disponibilidad de PostgreSQL y sus esquemas.</summary>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/ready").AllowAnonymous();
    }
}
