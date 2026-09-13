using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetPokemonSharingMove;

namespace Pokemon.Api.Feature.Pokedex.Moves.Queries;

/// <summary>
/// Lista ejemplares que tienen aprendido este movimiento.
/// </summary>
internal static class GetPokemonSharingMoveEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/pokemon", HandleAsync)
            .Produces<IReadOnlyList<PokemonView>>()
            .WithSummary("Lista ejemplares que tienen aprendido este movimiento")
            .WithDescription("Devuelve una página de ejemplares que tienen aprendido este movimiento, ordenados por identificador. Admite offset y limit (1–100).");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token, int? offset, int? limit) =>
        Results.Ok(await sender.Send(new GetPokemonSharingMoveQuery(id, offset ?? 0, limit ?? 100), token));
}
