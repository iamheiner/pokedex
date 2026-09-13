using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Queries.ListPokemon;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Queries;

/// <summary>Lista ejemplares.</summary>
internal static class ListPokemonEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/", HandleAsync)
            .Produces<IReadOnlyList<PokemonView>>()
            .WithSummary("Lista ejemplares")
            .WithDescription("Devuelve una página ordenada por nombre e identificador. offset empieza en 0 y limit admite entre 1 y 100 elementos; el límite predeterminado es 100.");
    }

    /// <summary>Lista ejemplares mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken token, int? offset, int? limit) =>
        Results.Ok(await sender.Send(new ListPokemonQuery(offset ?? 0, limit ?? 100), token));
}
