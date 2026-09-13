using Pokemon.Api.Feature.Battle.Commands;
using Pokemon.Api.Feature.Battle.Queries;

namespace Pokemon.Api.Feature.Battle;

/// <summary>Agrupa las rutas del recurso battles y sus contratos de error comunes.</summary>
public static class BattleEndpoints
{
    /// <summary>Registra las consultas y comandos HTTP del recurso battles.</summary>
    public static void MapBattleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/battles")
                             .WithTags("Battle")
                             .ProducesProblem(400)
                             .ProducesProblem(404)
                             .ProducesProblem(409)
                             .ProducesProblem(415)
                             .ProducesProblem(500);

        GetBattleEndpoint.Map(group);
        CreateBattleEndpoint.Map(group);
        PlayTurnEndpoint.Map(group);
    }
}
