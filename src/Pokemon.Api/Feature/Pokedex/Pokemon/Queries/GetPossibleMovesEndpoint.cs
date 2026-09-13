using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetPossibleMoves;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Queries;

/// <summary>Obtiene el plan de aprendizaje completo, incluidos niveles futuros.</summary>
internal static class GetPossibleMovesEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/possible-moves", HandleAsync)
            .Produces<PossibleMovesView>()
            .WithSummary("Obtiene el plan de aprendizaje completo, incluidos niveles futuros")
            .WithDescription("Devuelve el plan de aprendizaje completo de la especie del ejemplar, incluidos movimientos disponibles a niveles futuros.");
    }

    /// <summary>Obtiene el plan de aprendizaje completo, incluidos niveles futuros mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new GetPossibleMovesQuery(id), token));
}
