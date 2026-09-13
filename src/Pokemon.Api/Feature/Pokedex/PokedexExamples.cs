using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain;
namespace Pokemon.Api.Feature.Pokedex;

/// <summary>
/// Ejemplos ejecutables en Scalar contra los identificadores del catálogo inicial.
/// </summary>
internal static class PokedexExamples
{
    /// <summary>
    /// Genera los identificadores estables utilizados por los ejemplos de Pokédex.
    /// </summary>
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<PokemonType>() }
    };
    public static readonly MoveInput Move = new("Example attack", 40, PokemonType.Normal);
    public static readonly SpeciesInput Species = new("Example species", PokemonType.Fire,
        new(39, 52, 43, 60, 50, 65), [new(Id(1), 1), new(Id(2), 4), new(Id(3), 12), new(Id(4), 17)]);
    public static readonly PokemonInput Pokemon = new(Id(101), "My Charmander", 20, 39, 39, [Id(1), Id(2), Id(3), Id(4)]);

    /// <summary>
    /// Añade un ejemplo JSON al cuerpo de petición de una operación OpenAPI.
    /// </summary>
    public static RouteHandlerBuilder WithPokedexExample<T>(this RouteHandlerBuilder builder, T input) =>
        builder.AddOpenApiOperationTransformer((operation, context, token) =>
        {
            if (operation.RequestBody?.Content?.TryGetValue("application/json", out var content) == true)
                content.Example = JsonSerializer.SerializeToNode(input, Json);
            return Task.CompletedTask;
        });
}
