using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.UpdateSpecies;

namespace Pokemon.Api.Feature.Pokedex.Species.Commands;

/// <summary>
/// Reemplaza los datos de una especie; requiere todos los campos.
/// </summary>
internal static class UpdateSpeciesEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", HandleAsync)
            .WithPokedexExample(PokedexExamples.Species)
            .Produces<SpeciesView>()
            .WithSummary("Reemplaza los datos de una especie; requiere todos los campos")
            .WithDescription("Reemplaza los datos y el plan de aprendizaje. Devuelve 409 si el nuevo plan invalida movimientos aprendidos por ejemplares existentes.");
    }

    private static async Task<IResult> HandleAsync(Guid id, SpeciesInput input, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new UpdateSpeciesCommand(id, input), token));
}
