using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.CreatePokemon;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Commands;

/// <summary>Crea un ejemplar.</summary>
internal static class CreatePokemonEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPost("/", HandleAsync)
            .WithPokedexExample(PokedexExamples.Pokemon)
            .Produces<PokemonView>(201)
            .WithSummary("Crea un ejemplar")
            .WithDescription("Valida especie, nivel, salud y exactamente cuatro movimientos distintos permitidos para ese nivel. Devuelve 201 y Location.");
    }

    /// <summary>Crea un ejemplar mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(PokemonInput input, ISender sender, CancellationToken token)
    {
        var result = await sender.Send(new CreatePokemonCommand(input), token);
        return Results.Created($"/pokemon/{result.Id}", result);
    }
}
