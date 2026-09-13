using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.ListMoves;

namespace Pokemon.Api.Feature.Pokedex.Moves.Queries;

/// <summary>Lista movimientos.</summary>
internal static class ListMovesEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/", HandleAsync)
            .Produces<IReadOnlyList<MoveView>>()
            .WithSummary("Lista movimientos")
            .WithDescription("Devuelve una página ordenada por nombre e identificador. offset empieza en 0 y limit admite entre 1 y 100 elementos; el límite predeterminado es 100.");
    }

    /// <summary>Lista movimientos mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken token, int? offset, int? limit) =>
        Results.Ok(await sender.Send(new ListMovesQuery(offset ?? 0, limit ?? 100), token));
}
