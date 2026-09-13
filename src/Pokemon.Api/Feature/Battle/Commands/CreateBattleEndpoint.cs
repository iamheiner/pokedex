using MediatR;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Application.Feature.Battle.Commands.CreateBattle;
using System.Text.Json;

namespace Pokemon.Api.Feature.Battle.Commands;

/// <summary>Inicia una partida entre dos ejemplares de la Pokédex.</summary>
internal static class CreateBattleEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPost("/", HandleAsync)
            .Produces<BattleView>(201)
            .WithSummary("Inicia una partida entre dos ejemplares de la Pokédex")
            .WithDescription("Captura datos y salud actuales. Empieza el más rápido; en empate, el primer ejemplar. La colección no se modifica.")
            .AddOpenApiOperationTransformer((operation, context, token) =>
            {
                if (operation.RequestBody?.Content?.TryGetValue("application/json", out var content) == true)
                    content.Example = JsonSerializer.SerializeToNode(new CreateBattleInput(
                        Guid.Parse("00000000-0000-0000-0000-000000000201"),
                        Guid.Parse("00000000-0000-0000-0000-000000000202")), new JsonSerializerOptions(JsonSerializerDefaults.Web));

                return Task.CompletedTask;
            });
    }

    /// <summary>Inicia una partida entre dos ejemplares de la Pokédex mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(CreateBattleInput input, ISender sender, CancellationToken token)
    {
        var battle = await sender.Send(new CreateBattleCommand(input), token);
        return Results.Created($"/battles/{battle.Id}", battle);
    }
}
