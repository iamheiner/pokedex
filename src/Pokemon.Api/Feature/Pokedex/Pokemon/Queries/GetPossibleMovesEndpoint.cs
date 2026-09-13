using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetPossibleMoves;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Queries;

/// <summary>
/// Obtiene el plan de aprendizaje completo, incluidos niveles futuros.
/// </summary>
internal static class GetPossibleMovesEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/possible-moves", HandleAsync)
            .Produces<PossibleMovesView>()
            .WithSummary("Obtiene el plan de aprendizaje completo, incluidos niveles futuros")
            .WithDescription("Devuelve el plan de aprendizaje completo de la especie del ejemplar, incluidos movimientos disponibles a niveles futuros.");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new GetPossibleMovesQuery(id), token));
}
