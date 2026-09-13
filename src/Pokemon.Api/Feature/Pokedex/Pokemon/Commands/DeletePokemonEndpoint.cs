using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.DeletePokemon;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Commands;

/// <summary>
/// Elimina un ejemplar; rechaza referencias en uso.
/// </summary>
internal static class DeletePokemonEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", HandleAsync)
            .Produces(204)
            .WithSummary("Elimina un ejemplar; rechaza referencias en uso")
            .WithDescription("Elimina el ejemplar y sus relaciones de aprendizaje. Devuelve 204 o 404 si no existe; las partidas conservan su instantánea.");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token)
    {
        await sender.Send(new DeletePokemonCommand(id), token);
        return Results.NoContent();
    }
}
