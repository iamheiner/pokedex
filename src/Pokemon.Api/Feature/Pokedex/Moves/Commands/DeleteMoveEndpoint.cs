using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.DeleteMove;

namespace Pokemon.Api.Feature.Pokedex.Moves.Commands;

/// <summary>
/// Elimina un movimiento; rechaza referencias en uso.
/// </summary>
internal static class DeleteMoveEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", HandleAsync)
            .Produces(204)
            .WithSummary("Elimina un movimiento; rechaza referencias en uso")
            .WithDescription("Elimina un movimiento existente. Devuelve 204; si un plan de aprendizaje o ejemplar lo referencia, responde 409.");
    }
    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token)
    {
        await sender.Send(new DeleteMoveCommand(id), token);
        return Results.NoContent();
    }
}
