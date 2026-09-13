using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Pokemon.Api.Feature.Authentication;

/// <summary>Expone en OpenAPI el mismo requisito Bearer que exige la política global.</summary>
public sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Añade al documento OpenAPI el esquema Bearer y sus requisitos de seguridad.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "Access token de Keycloak destinado a pokemon-api."
        };
        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations?.Values.AsEnumerable() ?? []))
        {
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            operation.Responses ??= new();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Token ausente, caducado o inválido." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Identidad sin permiso para esta operación." });
        }
        return Task.CompletedTask;
    }
}
