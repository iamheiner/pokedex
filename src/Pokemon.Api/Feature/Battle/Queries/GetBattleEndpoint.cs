using MediatR;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Application.Feature.Battle.Queries.GetBattle;

namespace Pokemon.Api.Feature.Battle.Queries;

/// <summary>
/// Consulta salud, fase, versión, siguiente actor e historial.
/// </summary>
internal static class GetBattleEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", HandleAsync)
            .Produces<BattleView>()
            .WithSummary("Consulta salud, fase, versión, siguiente actor e historial")
            .WithDescription("Devuelve la instantánea actual de la partida, su versión, siguiente actor, resultado e historial sin ejecutar un turno.");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new GetBattleQuery(id), token));
}
