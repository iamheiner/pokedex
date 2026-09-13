using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Pokemon.Api.Feature.Damage;

// Una única fuente de ejemplos para Scalar y pruebas, incluida en la imagen publicada.
internal static class DamageExample
{
    /// <summary>
    /// Carga los ejemplos de daño del recurso JSON incluido en la aplicación.
    /// </summary>
    internal static Dictionary<string, IOpenApiExample> Load()
    {
        using var stream = typeof(DamageExample).Assembly.GetManifestResourceStream("DamageExamples.json")
            ?? throw new InvalidOperationException("Damage examples are missing.");
        var examples = JsonNode.Parse(stream)!.AsObject();
        return examples.ToDictionary(entry => entry.Key, entry => (IOpenApiExample)new OpenApiExample
        {
            Summary = entry.Key,
            Value = entry.Value!.DeepClone()
        });
    }
}
