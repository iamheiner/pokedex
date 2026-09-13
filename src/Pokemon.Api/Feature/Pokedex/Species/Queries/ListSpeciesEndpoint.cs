using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.ListSpecies;

namespace Pokemon.Api.Feature.Pokedex.Species.Queries;

/// <summary>
/// Lista especies.
/// </summary>
internal static class ListSpeciesEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/", HandleAsync)
            .Produces<IReadOnlyList<SpeciesView>>()
            .WithSummary("Lista especies")
            .WithDescription("Devuelve una página ordenada por nombre e identificador. offset empieza en 0 y limit admite entre 1 y 100 elementos; el límite predeterminado es 100.");
    }

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken token, int? offset, int? limit) =>
        Results.Ok(await sender.Send(new ListSpeciesQuery(offset ?? 0, limit ?? 100), token));
}
