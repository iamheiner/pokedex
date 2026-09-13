using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetPokemon;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Queries;

/// <summary>
/// Consulta un ejemplar por identidad.
/// </summary>
internal static class GetPokemonEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", HandleAsync)
            .Produces<PokemonView>()
            .WithSummary("Consulta un ejemplar por identidad")
            .WithDescription("Devuelve el ejemplar, su especie, estadísticas y cuatro movimientos aprendidos. Si no existe, responde 404.");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new GetPokemonQuery(id), token));
}
