using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.GetSpecies;

namespace Pokemon.Api.Feature.Pokedex.Species.Queries;

/// <summary>
/// Consulta una especie por identidad.
/// </summary>
internal static class GetSpeciesEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", HandleAsync)
            .Produces<SpeciesView>()
            .WithSummary("Consulta una especie por identidad")
            .WithDescription("Devuelve las estadísticas de la especie y su plan de aprendizaje por nivel. Si no existe, responde 404.");
    }

    private static async Task<IResult> HandleAsync(Guid id, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new GetSpeciesQuery(id), token));
}
