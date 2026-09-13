namespace Pokemon.Api.Feature.Health.Queries;

/// <summary>Indica que el proceso HTTP está en ejecución sin consultar sus dependencias.</summary>
internal static class GetHealthEndpoint
{
    /// <summary>Registra la sonda de actividad anónima con su descripción de OpenAPI.</summary>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", Handle)
            .AllowAnonymous()
            .WithSummary("Comprueba que la API responde")
            .WithDescription("Sonda de actividad del proceso; no comprueba la disponibilidad de PostgreSQL.");
    }

    /// <summary>Devuelve el estado de actividad del proceso sin acceder a datos de negocio.</summary>
    private static IResult Handle() => Results.Ok(new { status = "healthy" });
}
