namespace Pokemon.Api.Feature.Health.Queries;

/// <summary>
/// Indica que el proceso HTTP está en ejecución sin consultar sus dependencias.
/// </summary>
internal static class GetHealthEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", Handle)
            .AllowAnonymous()
            .WithSummary("Comprueba que la API responde")
            .WithDescription("Sonda de actividad del proceso; no comprueba la disponibilidad de PostgreSQL.");
    }

    private static IResult Handle() => Results.Ok(new { status = "healthy" });
}
