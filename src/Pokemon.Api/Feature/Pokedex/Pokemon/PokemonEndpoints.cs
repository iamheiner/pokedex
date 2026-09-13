using Pokemon.Api.Feature.Pokedex.Pokemon.Commands;
using Pokemon.Api.Feature.Pokedex.Pokemon.Queries;

namespace Pokemon.Api.Feature.Pokedex.Pokemon;

/// <summary>
/// Agrupa las rutas del recurso pokemon y sus contratos de error comunes.
/// </summary>
public static class PokemonEndpoints
{
    public static void MapPokemonEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/pokemon")
                             .WithTags("Pokedex - Pokemon")
                             .ProducesProblem(400)
                             .ProducesProblem(404)
                             .ProducesProblem(409)
                             .ProducesProblem(415)
                             .ProducesProblem(500);

        ListPokemonEndpoint.Map(group);
        GetPokemonEndpoint.Map(group);
        GetLearnedMovesEndpoint.Map(group);
        GetPossibleMovesEndpoint.Map(group);

        CreatePokemonEndpoint.Map(group);
        UpdatePokemonEndpoint.Map(group);
        DeletePokemonEndpoint.Map(group);
    }
}
