using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.CreateMove;

namespace Pokemon.Api.Feature.Pokedex.Moves.Commands;

/// <summary>Crea un movimiento.</summary>
internal static class CreateMoveEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPost("/", HandleAsync)
            .WithPokedexExample(PokedexExamples.Move)
            .Produces<MoveView>(201)
            .WithSummary("Crea un movimiento")
            .WithDescription("Valida nombre, potencia y tipo. Devuelve 201 y Location; un nombre duplicado produce 409.");
    }

    /// <summary>Crea un movimiento mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(MoveInput input, ISender sender, CancellationToken token)
    {
        var result = await sender.Send(new CreateMoveCommand(input), token);
        return Results.Created($"/moves/{result.Id}", result);
    }
}
