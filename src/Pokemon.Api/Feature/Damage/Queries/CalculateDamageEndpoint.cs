using Pokemon.Application.Common.Exceptions;
using Pokemon.Domain;
using Pokemon.Api.Feature.Damage.Contracts;
using MediatR;
using Pokemon.Application.Feature.Damage.Queries.CalculateDamage;

namespace Pokemon.Api.Feature.Damage.Queries;

/// <summary>Traduce las peticiones HTTP de daño al caso de uso y sus errores a HTTP 400.</summary>
internal static class CalculateDamageEndpoint
{
    /// <summary>Registra el cálculo de daño y sus ejemplos y respuestas en la documentación OpenAPI.</summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/damage", HandleAsync)
            .WithName("CalculateDamage")
            .WithTags("Damage")
            .WithSummary("Calcula el daño de un movimiento aprendido")
            .WithDescription("Devuelve daño teórico sin modificar salud. Efectividad según tipo del movimiento y defensor; factor aleatorio de 85 a 100.")
            .Produces<DamageResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .AddOpenApiOperationTransformer((operation, context, token) =>
            {
                if (operation.RequestBody?.Content?.TryGetValue("application/json", out var content) == true)
                    content.Examples = DamageExample.Load();
                return Task.CompletedTask;
            });
        return endpoints;
    }

    /// <summary>Transforma la petición HTTP, solicita el cálculo mediante MediatR y devuelve el resultado.</summary>
    private static async Task<IResult> HandleAsync(DamageRequest request, ISender sender, CancellationToken cancellationToken)
    {
        CalculateDamageQuery query;
        try
        {
            ArgumentNullException.ThrowIfNull(request.Attacker);
            ArgumentNullException.ThrowIfNull(request.Defender);
            query = new CalculateDamageQuery(request.Attacker.ToDomain(), request.MoveName, request.Defender.ToDomain());
        }
        catch (ArgumentException error)
        {
            // Solo los errores al construir el dominio desde datos HTTP son del cliente.
            throw new InvalidDamageRequestException(error.Message);
        }

        // Un fallo interno del handler/proveedor aleatorio debe conservar su categoría 500.
        var result = await sender.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
