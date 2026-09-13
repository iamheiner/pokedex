namespace Pokemon.Api.Feature.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        endpoints.MapHealthChecks("/health/ready");
        return endpoints;
    }
}
