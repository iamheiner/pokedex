using Pokemon.Api.Feature.Pokedex.Species.Commands;
using Pokemon.Api.Feature.Pokedex.Species.Queries;

namespace Pokemon.Api.Feature.Pokedex.Species;

/// <summary>Agrupa las rutas del recurso species y sus contratos de error comunes.</summary>
public static class SpeciesEndpoints
{
    /// <summary>Registra las consultas y comandos HTTP del recurso species.</summary>
    public static void MapSpeciesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/species").WithTags("Pokedex - Species")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(415).ProducesProblem(500);

        ListSpeciesEndpoint.Map(group);
        GetSpeciesEndpoint.Map(group);

        CreateSpeciesEndpoint.Map(group);
        UpdateSpeciesEndpoint.Map(group);
        DeleteSpeciesEndpoint.Map(group);
    }
}
