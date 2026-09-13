using MediatR;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Application.Feature.Battle.Commands.PlayTurn;

namespace Pokemon.Api.Feature.Battle.Commands;

/// <summary>
/// Resuelve una acción y devuelve el nuevo estado.
/// </summary>
internal static class PlayTurnEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPost("/{id:guid}/turns", HandleAsync)
            .Produces<BattleView>()
            .WithSummary("Resuelve una acción y devuelve el nuevo estado")
            .WithDescription("Envía nextPokemonId, un moveId con usos y expectedVersion igual a version. Al agotar todos los movimientos, envía moveId: null para esfuerzo. Una versión antigua o actuar después del final devuelve 409.");
    }

    private static async Task<IResult> HandleAsync(Guid id, PlayTurnInput input, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new PlayTurnCommand(id, input), token));
}
