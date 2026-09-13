using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetSpeciesSharingMove;

namespace Pokemon.Api.Feature.Pokedex.Moves.Queries;

/// <summary>Lista especies que pueden aprender este movimiento.</summary>
internal static class GetSpeciesSharingMoveEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/species", HandleAsync)
            .Produces<IReadOnlyList<SpeciesView>>()
            .WithSummary("Lista especies que pueden aprender este movimiento")
            .WithDescription("Devuelve una página de especies cuyo plan incluye este movimiento, ordenadas por identificador. Admite offset y limit (1–100).");
    }

    /// <summary>Lista especies que pueden aprender este movimiento mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token, int? offset, int? limit) =>
        Results.Ok(await sender.Send(new GetSpeciesSharingMoveQuery(id, offset ?? 0, limit ?? 100), token));
}
