using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.CreateSpecies;
using Pokemon.Application.Feature.Pokedex.Commands.UpdateSpecies;
using Pokemon.Application.Feature.Pokedex.Commands.DeleteSpecies;
using Pokemon.Application.Feature.Pokedex.Queries.GetSpecies;
using Pokemon.Application.Feature.Pokedex.Queries.ListSpecies;
namespace Pokemon.Api.Feature.Pokedex.Species;

/// <summary>Adaptadores HTTP del recurso species; MediatR ejecuta los casos de uso.</summary>
public static class SpeciesEndpoints
{
    /// <summary>Registra las rutas de consulta y mantenimiento de especies mediante MediatR.</summary>
    public static void MapSpeciesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/species").WithTags("Pokedex - Species")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(415).ProducesProblem(500);
        group.MapGet("/", async (ISender sender, CancellationToken token, int? offset, int? limit) =>
            Results.Ok(await sender.Send(new ListSpeciesQuery(offset ?? 0, limit ?? 100), token)))
            .Produces<IReadOnlyList<SpeciesView>>().WithSummary("Lista especies");
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new GetSpeciesQuery(id), token)))
            .Produces<SpeciesView>().WithSummary("Consulta una especie por identidad");
        group.MapPost("/", async (SpeciesInput input, ISender sender, CancellationToken token) =>
        {
            var result = await sender.Send(new CreateSpeciesCommand(input), token);
            return Results.Created($"/species/{result.Id}", result);
        }).WithPokedexExample(PokedexExamples.Species).Produces<SpeciesView>(201).WithSummary("Crea una especie");
        group.MapPut("/{id:guid}", async (Guid id, SpeciesInput input, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new UpdateSpeciesCommand(id, input), token)))
            .WithPokedexExample(PokedexExamples.Species).Produces<SpeciesView>().WithSummary("Reemplaza los datos de una especie; requiere todos los campos");
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken token) =>
        {
            await sender.Send(new DeleteSpeciesCommand(id), token);
            return Results.NoContent();
        }).Produces(204).WithSummary("Elimina una especie; rechaza referencias en uso");
    }
}
