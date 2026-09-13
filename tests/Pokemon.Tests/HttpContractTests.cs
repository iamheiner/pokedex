using Pokemon.Domain.Common.Persistence;
using Pokemon.Tests.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pokemon.Application.Feature.Damage;
using Pokemon.Domain;

namespace Pokemon.Tests;

public sealed class HttpContractTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;
    /// <summary>
    /// Conserva el host compartido que utilizarán las pruebas del contrato HTTP.
    /// </summary>
    public HttpContractTests(ApiFactory factory) => this.factory = factory;

    /// <summary>
    /// Enumera los campos obligatorios del contrato HTTP de cálculo de daño.
    /// </summary>
    public static IEnumerable<object[]> RequiredFields()
    {
        yield return ["attacker"];
        yield return ["defender"];
        yield return ["moveName"];
        var sample = Sample();
        foreach (var actor in new[] { "attacker", "defender" })
            foreach (var field in sample[actor]!.AsObject()) yield return [$"{actor}.{field.Key}"];
        foreach (var field in sample["attacker"]!["moves"]![0]!.AsObject()) yield return [$"attacker.moves.0.{field.Key}"];
    }

    /// <summary>
    /// Comprueba que omitir cualquier campo obligatorio produce un error de validación.
    /// </summary>
    [Theory]
    [MemberData(nameof(RequiredFields))]
    public async Task EveryContractFieldMustBePresent(string path)
    {
        var sample = Sample();
        Remove(sample, path);
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que un JSON inválido devuelve un error en formato ProblemDetails.
    /// </summary>
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    public Task InvalidJsonHasProblemDetails(string json) => AssertProblem(json, HttpStatusCode.BadRequest);

    /// <summary>
    /// Comprueba que los valores de texto inválidos devuelven un error en formato ProblemDetails.
    /// </summary>
    [Theory]
    [InlineData("type", "Unknown")]
    [InlineData("name", "")]
    public async Task InvalidTextHasProblemDetails(string property, string value)
    {
        var sample = Sample();
        sample["attacker"]![property] = value;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que las referencias obligatorias no aceptan valores nulos.
    /// </summary>
    [Theory]
    [InlineData("attacker")]
    [InlineData("defender")]
    [InlineData("moveName")]
    public async Task NullRequiredReferenceIsRejected(string property)
    {
        var sample = Sample(); sample[property] = null;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que un nombre de movimiento vacío devuelve un error de validación.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyMoveNameHasProblemDetails(string value)
    {
        var sample = Sample(); sample["moveName"] = value;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que las propiedades JSON desconocidas se rechazan.
    /// </summary>
    [Fact]
    public async Task UnknownPropertyIsRejected()
    {
        var sample = Sample(); sample["attacker"]!["typo"] = "Fire";
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que un movimiento nulo en la petición HTTP se rechaza.
    /// </summary>
    [Fact]
    public async Task NullMoveIsRejected()
    {
        var sample = Sample(); sample["attacker"]!["moves"]![0] = null;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que los valores numéricos fuera de contrato se rechazan por HTTP.
    /// </summary>
    [Theory]
    [InlineData("level", 0)]
    [InlineData("level", 101)]
    [InlineData("defense", 0)]
    [InlineData("currentHealth", -1)]
    [InlineData("currentHealth", 10001)]
    [InlineData("type", 0)]
    public async Task InvalidNumbersAreRejected(string field, int value)
    {
        var sample = Sample(); sample["attacker"]![field] = value;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que enviar salud cero explícita es válido para el cálculo teórico de daño.
    /// </summary>
    [Fact]
    public async Task ExplicitZeroHealthIsValidForTheoreticalDamage()
    {
        var sample = Sample(); sample["attacker"]!["currentHealth"] = 0;
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/damage", Json(sample.ToJsonString()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Comprueba que seleccionar un movimiento no aprendido devuelve ProblemDetails.
    /// </summary>
    [Fact]
    public async Task UnlearnedMoveHasProblemDetails()
    {
        var sample = Sample(); sample["moveName"] = "Unknown";
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Comprueba que un tipo de contenido no admitido devuelve el error HTTP correspondiente.
    /// </summary>
    [Fact]
    public async Task UnsupportedContentTypeHasProblemDetails() =>
        await AssertProblem(Sample().ToJsonString(), HttpStatusCode.UnsupportedMediaType, "text/plain");

    /// <summary>
    /// Comprueba que un fallo del proveedor aleatorio se devuelve como error interno y no como validación del cliente.
    /// </summary>
    [Fact]
    public async Task InternalRandomFailureIs500NotClientError()
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDamageRandom>(); services.AddSingleton<IDamageRandom>(new FixedRandom(101));
        }));
        using var client = broken.CreateClient();
        var response = await client.PostAsync("/damage", Json(Sample().ToJsonString()));
        await CheckProblem(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain("randomFactor", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Comprueba los seis ejemplos de daño a través del host HTTP y MediatR reales.
    /// </summary>
    [Theory]
    [InlineData("weakness", 2, 39)]
    [InlineData("resistance", 0.5, 7)]
    [InlineData("immunity", 0, 0)]
    [InlineData("neutral", 1, 19)]
    [InlineData("fighting", 2, 91)]
    [InlineData("ground", 2, 57)]
    public async Task AllSixExamplesExecuteThroughHttpAndMediator(string name, double effectiveness, int damage)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/damage", Json(Sample(name).ToJsonString()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DamageResult>();
        Assert.NotNull(result);
        Assert.Equal((decimal)effectiveness, result.Effectiveness);
        Assert.Equal(damage, result.Damage);
        Assert.Equal(100, result.RandomFactor);
    }

    /// <summary>
    /// Comprueba que OpenAPI contiene todos los ejemplos y contratos de error del cálculo.
    /// </summary>
    [Fact]
    public async Task DocumentationContainsAllExamplesAndErrorContracts()
    {
        using var client = factory.CreateClient();
        var spec = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        var operation = spec["paths"]!["/damage"]!["post"]!;
        Assert.Equal(6, operation["requestBody"]!["content"]!["application/json"]!["examples"]!.AsObject().Count);
        foreach (var code in new[] { "400", "415", "500" })
            Assert.NotNull(operation["responses"]![code]!["content"]!["application/problem+json"]);
        Assert.Contains("scalar", await client.GetStringAsync("/scalar/v1"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Comprueba que todas las operaciones de negocio publican resumen y descripción en OpenAPI.
    /// </summary>
    [Fact]
    public async Task EveryBusinessOperationHasSummaryAndDescription()
    {
        using var client = factory.CreateClient();
        var spec = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        var resources = new[] { "damage", "moves", "species", "pokemon", "battles" };
        var operationCount = 0;
        foreach (var path in spec["paths"]!.AsObject())
        {
            if (!resources.Contains(path.Key.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()))
                continue;

            foreach (var method in new[] { "get", "post", "put", "delete" })
            {
                if (path.Value?[method] is not JsonObject operation)
                    continue;

                var route = $"{method.ToUpperInvariant()} {path.Key}";
                Assert.False(string.IsNullOrWhiteSpace(operation["summary"]?.GetValue<string>()), $"Falta el resumen de {route}.");
                Assert.False(string.IsNullOrWhiteSpace(operation["description"]?.GetValue<string>()), $"Falta la descripción de {route}.");
                operationCount++;
            }
        }

        Assert.Equal(23, operationCount);
    }

    /// <summary>
    /// Envía una petición de daño y comprueba su respuesta de error esperada.
    /// </summary>
    private async Task AssertProblem(string json, HttpStatusCode status, string mediaType = "application/json")
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/damage", new StringContent(json, Encoding.UTF8, mediaType));
        await CheckProblem(response, status);
    }

    /// <summary>
    /// Valida el estado HTTP, el formato ProblemDetails y la presencia del identificador de traza.
    /// </summary>
    private static async Task CheckProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal((int)status, problem["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(problem["title"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]!.GetValue<string>()));
    }

    /// <summary>
    /// Crea contenido HTTP JSON codificado en UTF-8 para las pruebas.
    /// </summary>
    private static StringContent Json(string text) => new(text, Encoding.UTF8, "application/json");
    /// <summary>
    /// Carga del fichero de pruebas el ejemplo de daño solicitado.
    /// </summary>
    private static JsonObject Sample(string name = "weakness") =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "damage-requests.json")))![name]!.AsObject();

    /// <summary>
    /// Elimina una propiedad de un objeto JSON siguiendo una ruta con propiedades e índices.
    /// </summary>
    private static void Remove(JsonNode node, string path)
    {
        var parts = path.Split('.');
        foreach (var part in parts.SkipLast(1)) node = int.TryParse(part, out var index) ? node[index]! : node[part]!;
        node.AsObject().Remove(parts[^1]);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Configura el cliente HTTP de pruebas con un token de autenticación válido.
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        Authentication.TestTokens.Authorize(client);
    }

    /// <summary>
    /// Configura el host de pruebas con autenticación controlada y dobles de persistencia y azar.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        Authentication.TestTokens.Configure(builder);
        builder.UseSetting("ConnectionStrings:Battles", "Host=unused-test-database;Database=pokemon");
        builder.UseSetting("ApiDocumentation:Enabled", "true");
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Pokemon.Domain.Common.Persistence.IUnitOfWork>();
            services.AddSingleton<Pokemon.Domain.Common.Persistence.IUnitOfWork>(_ => new InMemoryUnitOfWork());
            services.RemoveAll<Pokemon.Domain.Common.Persistence.IReadSession>();
            services.AddSingleton<Pokemon.Domain.Common.Persistence.IReadSession>(provider =>
                (Pokemon.Domain.Common.Persistence.IReadSession)provider.GetRequiredService<Pokemon.Domain.Common.Persistence.IUnitOfWork>());
            foreach (var descriptor in services.Where(d => d.ImplementationType == typeof(Pokemon.Infrastructure.PersistenceMigrationService)).ToArray())
                services.Remove(descriptor);
            services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(options => options.Registrations.Clear());
            services.RemoveAll<IDamageRandom>(); services.AddSingleton<IDamageRandom>(new FixedRandom(100));
        });
    }
}

internal sealed class FixedRandom(int value) : IDamageRandom
{
    /// <summary>
    /// Devuelve el factor fijo configurado para el escenario de prueba.
    /// </summary>
    public int Next() => value;
}
