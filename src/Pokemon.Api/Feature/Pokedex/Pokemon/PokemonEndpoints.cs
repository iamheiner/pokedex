using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.CreatePokemon;
using Pokemon.Application.Feature.Pokedex.Commands.UpdatePokemon;
using Pokemon.Application.Feature.Pokedex.Commands.DeletePokemon;
using Pokemon.Application.Feature.Pokedex.Queries.GetPokemon;
using Pokemon.Application.Feature.Pokedex.Queries.ListPokemon;
using Pokemon.Application.Feature.Pokedex.Queries.GetLearnedMoves;
using Pokemon.Application.Feature.Pokedex.Queries.GetPossibleMoves;
namespace Pokemon.Api.Feature.Pokedex.Pokemon;

/// <summary>Adaptadores HTTP del recurso pokemon; MediatR ejecuta los casos de uso.</summary>
public static class PokemonEndpoints
{
    public static void MapPokemonEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/pokemon").WithTags("Pokedex - Pokemon")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(415).ProducesProblem(500);
        group.MapGet("/", async (ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new ListPokemonQuery(), token)))
            .Produces<IReadOnlyList<PokemonView>>().WithSummary("Lista ejemplares");
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new GetPokemonQuery(id), token)))
            .Produces<PokemonView>().WithSummary("Consulta un ejemplar por identidad");
        group.MapPost("/", async (PokemonInput input, ISender sender, CancellationToken token) =>
        {
            var result = await sender.Send(new CreatePokemonCommand(input), token);
            return Results.Created($"/pokemon/{result.Id}", result);
        }).WithPokedexExample(PokedexExamples.Pokemon).Produces<PokemonView>(201).WithSummary("Crea un ejemplar");
        group.MapPut("/{id:guid}", async (Guid id, PokemonInput input, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new UpdatePokemonCommand(id, input), token)))
            .WithPokedexExample(PokedexExamples.Pokemon).Produces<PokemonView>().WithSummary("Reemplaza los datos de un ejemplar; requiere todos los campos");
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken token) =>
        {
            await sender.Send(new DeletePokemonCommand(id), token);
            return Results.NoContent();
        }).Produces(204).WithSummary("Elimina un ejemplar; rechaza referencias en uso");

        group.MapGet("/{id:guid}/moves", async (Guid id, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new GetLearnedMovesQuery(id), token)))
            .Produces<PokemonView>().WithSummary("Obtiene un ejemplar junto a sus cuatro movimientos aprendidos");
        group.MapGet("/{id:guid}/possible-moves", async (Guid id, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new GetPossibleMovesQuery(id), token)))
            .Produces<PossibleMovesView>().WithSummary("Obtiene el plan de aprendizaje completo, incluidos niveles futuros");
    }
}
