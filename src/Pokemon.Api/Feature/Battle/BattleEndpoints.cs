using MediatR;
using System.Text.Json;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Application.Feature.Battle.Commands.CreateBattle;
using Pokemon.Application.Feature.Battle.Commands.PlayTurn;
using Pokemon.Application.Feature.Battle.Queries.GetBattle;
namespace Pokemon.Api.Feature.Battle;

/// <summary>API de partidas; las reglas de combate y concurrencia pertenecen a los casos de uso y al dominio.</summary>
public static class BattleEndpoints
{
    /// <summary>Registra las rutas HTTP para crear partidas, consultar su estado y resolver turnos mediante MediatR.</summary>
    public static void MapBattleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/battles").WithTags("Battle")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(415).ProducesProblem(500);
        group.MapPost("/", async (CreateBattleInput input, ISender sender, CancellationToken token) =>
        {
            var battle = await sender.Send(new CreateBattleCommand(input), token);
            return Results.Created($"/battles/{battle.Id}", battle);
        }).Produces<BattleView>(201).WithSummary("Inicia una partida entre dos ejemplares de la Pokédex")
            .WithDescription("Captura datos y salud actuales. Empieza el más rápido; en empate, el primer ejemplar. La colección no se modifica.")
            .AddOpenApiOperationTransformer((operation, context, token) =>
            {
                if (operation.RequestBody?.Content?.TryGetValue("application/json", out var content) == true)
                    content.Example = JsonSerializer.SerializeToNode(new CreateBattleInput(
                        Guid.Parse("00000000-0000-0000-0000-000000000201"),
                        Guid.Parse("00000000-0000-0000-0000-000000000202")), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return Task.CompletedTask;
            });
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new GetBattleQuery(id), token)))
            .Produces<BattleView>().WithSummary("Consulta salud, fase, versión, siguiente actor e historial");
        group.MapPost("/{id:guid}/turns", async (Guid id, PlayTurnInput input, ISender sender, CancellationToken token) =>
            Results.Ok(await sender.Send(new PlayTurnCommand(id, input), token)))
            .Produces<BattleView>().WithSummary("Resuelve una acción y devuelve el nuevo estado")
            .WithDescription("Envía nextPokemonId, un moveId con usos y expectedVersion igual a version. Al agotar todos los movimientos, envía moveId: null para esfuerzo. Una versión antigua o actuar después del final devuelve 409.");
    }
}
