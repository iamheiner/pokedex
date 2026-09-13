namespace Pokemon.Api.Feature.Health;

public static class HealthEndpoints
{
    /// <summary>
    /// Sondas anónimas: un orquestador o balanceador no puede adjuntar un Bearer.
    /// No exponen datos de negocio; readiness solo indica si PostgreSQL y su esquema responden.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready").AllowAnonymous();
        return endpoints;
    }
}
