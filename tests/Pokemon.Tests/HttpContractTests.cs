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
    public HttpContractTests(ApiFactory factory) => this.factory = factory;

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

    [Theory]
    [MemberData(nameof(RequiredFields))]
    public async Task EveryContractFieldMustBePresent(string path)
    {
        var sample = Sample();
        Remove(sample, path);
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    public Task InvalidJsonHasProblemDetails(string json) => AssertProblem(json, HttpStatusCode.BadRequest);

    [Theory]
    [InlineData("type", "Unknown")]
    [InlineData("name", "")]
    public async Task InvalidTextHasProblemDetails(string property, string value)
    {
        var sample = Sample();
        sample["attacker"]![property] = value;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("attacker")]
    [InlineData("defender")]
    [InlineData("moveName")]
    public async Task NullRequiredReferenceIsRejected(string property)
    {
        var sample = Sample(); sample[property] = null;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyMoveNameHasProblemDetails(string value)
    {
        var sample = Sample(); sample["moveName"] = value;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownPropertyIsRejected()
    {
        var sample = Sample(); sample["attacker"]!["typo"] = "Fire";
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NullMoveIsRejected()
    {
        var sample = Sample(); sample["attacker"]!["moves"]![0] = null;
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

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

    [Fact]
    public async Task ExplicitZeroHealthIsValidForTheoreticalDamage()
    {
        var sample = Sample(); sample["attacker"]!["currentHealth"] = 0;
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/damage", Json(sample.ToJsonString()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnlearnedMoveHasProblemDetails()
    {
        var sample = Sample(); sample["moveName"] = "Unknown";
        await AssertProblem(sample.ToJsonString(), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnsupportedContentTypeHasProblemDetails() =>
        await AssertProblem(Sample().ToJsonString(), HttpStatusCode.UnsupportedMediaType, "text/plain");

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

    private async Task AssertProblem(string json, HttpStatusCode status, string mediaType = "application/json")
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/damage", new StringContent(json, Encoding.UTF8, mediaType));
        await CheckProblem(response, status);
    }

    private static async Task CheckProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal((int)status, problem["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(problem["title"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]!.GetValue<string>()));
    }

    private static StringContent Json(string text) => new(text, Encoding.UTF8, "application/json");
    private static JsonObject Sample(string name = "weakness") =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "damage-requests.json")))![name]!.AsObject();

    private static void Remove(JsonNode node, string path)
    {
        var parts = path.Split('.');
        foreach (var part in parts.SkipLast(1)) node = int.TryParse(part, out var index) ? node[index]! : node[part]!;
        node.AsObject().Remove(parts[^1]);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        Authentication.TestTokens.Authorize(client);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        Authentication.TestTokens.Configure(builder);
        builder.UseSetting("ConnectionStrings:Battles", "Host=unused-test-database;Database=pokemon");
        builder.UseSetting("ApiDocumentation:Enabled", "true");
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Pokemon.Domain.Battle.Repositories.IBattleRepository>();
            services.AddSingleton<Pokemon.Domain.Battle.Repositories.IBattleRepository, InMemoryBattleRepository>();
            services.RemoveAll<Pokemon.Domain.Pokedex.Repositories.IPokedexUnitOfWork>();
            services.AddSingleton<Pokemon.Domain.Pokedex.Repositories.IPokedexUnitOfWork>(_ => new InMemoryPokedexUnitOfWork());
            services.RemoveAll<Pokemon.Domain.Pokedex.Repositories.IPokedexReadSession>();
            services.AddSingleton<Pokemon.Domain.Pokedex.Repositories.IPokedexReadSession>(provider =>
                (Pokemon.Domain.Pokedex.Repositories.IPokedexReadSession)provider.GetRequiredService<Pokemon.Domain.Pokedex.Repositories.IPokedexUnitOfWork>());
            foreach (var descriptor in services.Where(d => d.ImplementationType == typeof(Pokemon.Infrastructure.PersistenceMigrationService)).ToArray())
                services.Remove(descriptor);
            services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(options => options.Registrations.Clear());
            services.RemoveAll<IDamageRandom>(); services.AddSingleton<IDamageRandom>(new FixedRandom(100));
        });
    }
}

internal sealed class FixedRandom(int value) : IDamageRandom
{
    public int Next() => value;
}
