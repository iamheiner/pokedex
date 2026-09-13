using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.CreateSpecies;

namespace Pokemon.Api.Feature.Pokedex.Species.Commands;

/// <summary>
/// Crea una especie.
/// </summary>
internal static class CreateSpeciesEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPost("/", HandleAsync)
            .WithPokedexExample(PokedexExamples.Species)
            .Produces<SpeciesView>(201)
            .WithSummary("Crea una especie")
            .WithDescription("Valida estadísticas, nombre único y movimientos del plan de aprendizaje. Devuelve 201 y Location.");
    }

    private static async Task<IResult> HandleAsync(SpeciesInput input, ISender sender, CancellationToken token)
    {
        var result = await sender.Send(new CreateSpeciesCommand(input), token);
        return Results.Created($"/species/{result.Id}", result);
    }
}
