using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.DeleteSpecies;

namespace Pokemon.Api.Feature.Pokedex.Species.Commands;

/// <summary>Elimina una especie; rechaza referencias en uso.</summary>
internal static class DeleteSpeciesEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", HandleAsync)
            .Produces(204)
            .WithSummary("Elimina una especie; rechaza referencias en uso")
            .WithDescription("Elimina la especie y su plan de aprendizaje. Devuelve 204; si existen ejemplares de esa especie, responde 409.");
    }

    /// <summary>Elimina una especie mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token)
    {
        await sender.Send(new DeleteSpeciesCommand(id), token);
        return Results.NoContent();
    }
}
