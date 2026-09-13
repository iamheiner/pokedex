namespace Pokemon.Api.Feature.Health.Queries;

/// <summary>
/// Comprueba mediante las sondas registradas si la API está preparada para atender peticiones.
/// </summary>
internal static class GetReadinessEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/ready").AllowAnonymous();
    }
}
