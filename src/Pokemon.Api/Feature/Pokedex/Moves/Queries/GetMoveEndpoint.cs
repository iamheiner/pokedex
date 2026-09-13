using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetMove;

namespace Pokemon.Api.Feature.Pokedex.Moves.Queries;

/// <summary>
/// Consulta un movimiento por identidad.
/// </summary>
internal static class GetMoveEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", HandleAsync)
            .Produces<MoveView>()
            .WithSummary("Consulta un movimiento por identidad")
            .WithDescription("Devuelve identidad, nombre, potencia y tipo del movimiento. Si no existe, responde 404.");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new GetMoveQuery(id), token));
}
