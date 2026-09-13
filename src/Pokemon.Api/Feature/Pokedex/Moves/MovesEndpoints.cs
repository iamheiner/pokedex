using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.CreateMove;
using Pokemon.Application.Feature.Pokedex.Commands.UpdateMove;
using Pokemon.Application.Feature.Pokedex.Commands.DeleteMove;
using Pokemon.Application.Feature.Pokedex.Queries.GetMove;
using Pokemon.Application.Feature.Pokedex.Queries.ListMoves;
using Pokemon.Application.Feature.Pokedex.Queries.GetPokemonSharingMove;
using Pokemon.Application.Feature.Pokedex.Queries.GetSpeciesSharingMove;
namespace Pokemon.Api.Feature.Pokedex.Moves;

/// <summary>Adaptadores HTTP del recurso moves; MediatR ejecuta los casos de uso.</summary>
public static class MovesEndpoints
{
    public static void MapMovesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/moves").WithTags("Pokedex - Moves")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(415).ProducesProblem(500);
        group.MapGet("/", async (ISender sender, CancellationToken token, int? offset, int? limit) =>
            Results.Ok(await sender.Send(new ListMovesQuery(offset ?? 0, limit ?? 100), token)))
            .Produces<IReadOnlyList<MoveView>>().WithSummary("Lista movimientos");
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new GetMoveQuery(id), token)))
            .Produces<MoveView>().WithSummary("Consulta un movimiento por identidad");
        group.MapPost("/", async (MoveInput input, ISender sender, CancellationToken token) =>
        {
            var result = await sender.Send(new CreateMoveCommand(input), token);
            return Results.Created($"/moves/{result.Id}", result);
        }).WithPokedexExample(PokedexExamples.Move).Produces<MoveView>(201).WithSummary("Crea un movimiento");
        group.MapPut("/{id:guid}", async (Guid id, MoveInput input, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new UpdateMoveCommand(id, input), token)))
            .WithPokedexExample(PokedexExamples.Move).Produces<MoveView>().WithSummary("Reemplaza los datos de un movimiento; requiere todos los campos");
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken token) =>
        {
            await sender.Send(new DeleteMoveCommand(id), token);
            return Results.NoContent();
        }).Produces(204).WithSummary("Elimina un movimiento; rechaza referencias en uso");

        group.MapGet("/{id:guid}/pokemon", async (Guid id, ISender sender, CancellationToken token, int? offset, int? limit) =>
            Results.Ok(await sender.Send(new GetPokemonSharingMoveQuery(id, offset ?? 0, limit ?? 100), token)))
            .Produces<IReadOnlyList<PokemonView>>().WithSummary("Lista ejemplares que tienen aprendido este movimiento");
        group.MapGet("/{id:guid}/species", async (Guid id, ISender sender, CancellationToken token, int? offset, int? limit) =>
            Results.Ok(await sender.Send(new GetSpeciesSharingMoveQuery(id, offset ?? 0, limit ?? 100), token)))
            .Produces<IReadOnlyList<SpeciesView>>().WithSummary("Lista especies que pueden aprender este movimiento");
    }
}
