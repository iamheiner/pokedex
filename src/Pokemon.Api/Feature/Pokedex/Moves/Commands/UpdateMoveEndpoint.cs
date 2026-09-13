using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.UpdateMove;

namespace Pokemon.Api.Feature.Pokedex.Moves.Commands;

/// <summary>
/// Reemplaza los datos de un movimiento; requiere todos los campos.
/// </summary>
internal static class UpdateMoveEndpoint
{
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", HandleAsync)
            .WithPokedexExample(PokedexExamples.Move)
            .Produces<MoveView>()
            .WithSummary("Reemplaza los datos de un movimiento; requiere todos los campos")
            .WithDescription("Reemplaza todos los datos del movimiento existente; el cambio se refleja en las consultas del catálogo. Un nombre duplicado produce 409.");
    }
    
    private static async Task<IResult> HandleAsync(Guid id, MoveInput input, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new UpdateMoveCommand(id, input), token));
}
