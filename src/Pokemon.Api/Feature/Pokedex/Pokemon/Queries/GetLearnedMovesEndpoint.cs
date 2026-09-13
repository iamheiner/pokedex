using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetLearnedMoves;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Queries;

/// <summary>
/// Obtiene un ejemplar junto a sus cuatro movimientos aprendidos.
/// </summary>
internal static class GetLearnedMovesEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/moves", HandleAsync)
            .Produces<PokemonView>()
            .WithSummary("Obtiene un ejemplar junto a sus cuatro movimientos aprendidos")
            .WithDescription("Devuelve el ejemplar junto con los cuatro movimientos que tiene actualmente aprendidos, en su orden asignado.");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new GetLearnedMovesQuery(id), token));
}
