using MediatR;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Application.Feature.Pokedex.Commands.UpdatePokemon;

namespace Pokemon.Api.Feature.Pokedex.Pokemon.Commands;

/// <summary>Reemplaza los datos de un ejemplar; requiere todos los campos.</summary>
internal static class UpdatePokemonEndpoint
{
    /// <summary>Registra la ruta HTTP, sus respuestas y los metadatos de OpenAPI.</summary>
    public static void Map(RouteGroupBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", HandleAsync)
            .WithPokedexExample(PokedexExamples.Pokemon)
            .Produces<PokemonView>()
            .WithSummary("Reemplaza los datos de un ejemplar; requiere todos los campos")
            .WithDescription("Reemplaza especie, nombre, nivel, salud y movimientos del ejemplar. Requiere todos los campos y aprendizaje válido.");
    }

    /// <summary>Reemplaza los datos de un ejemplar mediante el caso de uso de Application.</summary>
    private static async Task<IResult> HandleAsync(Guid id, PokemonInput input, ISender sender, CancellationToken token) =>
        Results.Ok(await sender.Send(new UpdatePokemonCommand(id, input), token));
}
