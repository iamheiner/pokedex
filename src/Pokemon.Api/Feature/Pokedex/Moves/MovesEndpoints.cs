using Pokemon.Api.Feature.Pokedex.Moves.Commands;
using Pokemon.Api.Feature.Pokedex.Moves.Queries;

namespace Pokemon.Api.Feature.Pokedex.Moves;

/// <summary>Agrupa las rutas del recurso moves y sus contratos de error comunes.</summary>
public static class MovesEndpoints
{
    /// <summary>Registra las consultas y comandos HTTP del recurso moves.</summary>
    public static void MapMovesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/moves").WithTags("Pokedex - Moves")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(415).ProducesProblem(500);

        ListMovesEndpoint.Map(group);
        GetMoveEndpoint.Map(group);
        GetPokemonSharingMoveEndpoint.Map(group);
        GetSpeciesSharingMoveEndpoint.Map(group);

        CreateMoveEndpoint.Map(group);
        UpdateMoveEndpoint.Map(group);
        DeleteMoveEndpoint.Map(group);
    }
}
