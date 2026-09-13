using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Pokemon.Application.Feature.Pokedex.Contracts;
using Pokemon.Domain;

namespace Pokemon.Tests.Pokedex;

public sealed class PokedexHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");
    private static object Input(string resource) => resource switch
    {
        "moves" => new MoveInput("Test move", 75, PokemonType.Water),
        "species" => new SpeciesInput("Test species", PokemonType.Fire, new(50, 60, 70, 80, 90, 100), [new(Id(1), 1)]),
        _ => new PokemonInput(Id(101), "Test pokemon", 20, 30, 39, [Id(1), Id(2), Id(3), Id(4)])
    };
    private static JsonObject Node(string resource) => JsonSerializer.SerializeToNode(Input(resource), Json)!.AsObject();
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object body) =>
        client.SendAsync(new HttpRequestMessage(method, path) { Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json") });
    private static async Task<JsonObject> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await Body(response);
        Assert.Equal((int)status, (int)problem["status"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem["traceId"]));
    }

    [Theory]
    [InlineData("moves")]
    [InlineData("species")]
    [InlineData("pokemon")]
    public async Task CrudRoundTripAndMissingResources(string resource)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var initial = (await client.GetFromJsonAsync<JsonArray>($"/{resource}"))!.Count;
        var created = await Send(client, HttpMethod.Post, $"/{resource}", Input(resource));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await Body(created); var id = (Guid)body["id"]!;
        Assert.Equal($"/{resource}/{id}", created.Headers.Location!.ToString());
        var fetched = await client.GetFromJsonAsync<JsonObject>($"/{resource}/{id}");
        Assert.Equal(body.ToJsonString(), fetched!.ToJsonString());
        Assert.Equal(initial + 1, (await client.GetFromJsonAsync<JsonArray>($"/{resource}"))!.Count);
        var update = Node(resource); update["name"] = "Updated name";
        var changed = await Send(client, HttpMethod.Put, $"/{resource}/{id}", update);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("Updated name", (string?)(await Body(changed))["name"]);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/{resource}/{id}")).StatusCode);
        await Problem(await client.GetAsync($"/{resource}/{id}"), HttpStatusCode.NotFound);
        await Problem(await client.DeleteAsync($"/{resource}/{id}"), HttpStatusCode.NotFound);
        await Problem(await Send(client, HttpMethod.Put, $"/{resource}/{id}", update), HttpStatusCode.NotFound);
        Assert.Equal(initial, (await client.GetFromJsonAsync<JsonArray>($"/{resource}"))!.Count);
    }

    public static IEnumerable<object[]> MandatoryFields()
    {
        foreach (var resource in new[] { "moves", "species", "pokemon" })
        {
            foreach (var property in Node(resource)) yield return [resource, property.Key];
            if (resource != "species") continue;
            foreach (var property in Node(resource)["stats"]!.AsObject()) yield return [resource, "stats." + property.Key];
            yield return [resource, "learnset.0.moveId"];
            yield return [resource, "learnset.0.level"];
        }
    }
    [Theory]
    [MemberData(nameof(MandatoryFields))]
    public async Task MissingFieldsNeverBecomeImplicitDefaults(string resource, string field)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var input = Node(resource); JsonNode current = input;
        var parts = field.Split('.');
        foreach (var part in parts.SkipLast(1)) current = int.TryParse(part, out var index) ? current[index]! : current[part]!;
        current.AsObject().Remove(parts[^1]);
        await Problem(await Send(client, HttpMethod.Post, $"/{resource}", input), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SeedHasFiveSpeciesAndFivePokemonWithFourLearnedMoves()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var species = (await client.GetFromJsonAsync<SpeciesView[]>("/species", Json))!;
        var pokemon = (await client.GetFromJsonAsync<PokemonView[]>("/pokemon", Json))!;
        Assert.Equal(5, species.Length); Assert.Equal(5, pokemon.Length);
        foreach (var p in pokemon)
        {
            var learned = (await client.GetFromJsonAsync<PokemonView>($"/pokemon/{p.Id}/moves", Json))!;
            Assert.Equal(p.Id, learned.Id); Assert.Equal(4, learned.Moves.Count);
            var possible = (await client.GetFromJsonAsync<PossibleMovesView>($"/pokemon/{p.Id}/possible-moves", Json))!;
            Assert.True(possible.Moves.Count > 4);
            Assert.All(learned.Moves, m => Assert.Contains(possible.Moves, e => e.Move.Id == m.Id && e.Level <= p.Level));
        }
        var charmander = pokemon.Single(p => p.SpeciesId == Id(101));
        Assert.Contains(charmander.Moves, m => m.Type == PokemonType.Dragon); // Distinto tipo también es compatible.
        var future = (await client.GetFromJsonAsync<PossibleMovesView>($"/pokemon/{charmander.Id}/possible-moves", Json))!;
        Assert.Contains(future.Moves, m => m.Move.Name == "Flamethrower" && m.Level == 24); // No filtrar por nivel actual20.
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(5)]
    public async Task OwnedPokemonRequiresExactlyFourMoves(int count)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var input = new PokemonInput(Id(101), "Invalid", 50, 20, 39, Enumerable.Range(1, count).Select(Id).ToArray());
        await Problem(await Send(client, HttpMethod.Post, "/pokemon", input), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("unlearnable")] [InlineData("future")]
    [InlineData("unknown-species")] [InlineData("unknown-move")] [InlineData("level")]
    [InlineData("health")] [InlineData("null-moves")]
    public async Task InvalidLearningAndHealthDoNotChangeExistingPokemon(string fault)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var path = $"/pokemon/{Id(201)}"; var before = await client.GetStringAsync(path);
        var input = Node("pokemon");
        switch (fault)
        {
            case "duplicate": input["moveIds"]![3] = Id(1); break;
            case "unlearnable": input["moveIds"]![3] = Id(6); break;
            case "future": input["moveIds"]![3] = Id(5); break;
            case "unknown-species": input["speciesId"] = Guid.NewGuid(); break;
            case "unknown-move": input["moveIds"]![3] = Guid.NewGuid(); break;
            case "level": input["level"] = 0; break;
            case "health": input["currentHealth"] = 40; break;
            case "null-moves": input["moveIds"] = null; break;
        }
        await Problem(await Send(client, HttpMethod.Put, path, input), HttpStatusCode.BadRequest);
        Assert.Equal(before, await client.GetStringAsync(path));
    }

    [Fact]
    public async Task InverseQueriesDistinguishLearnedFromPossibleAndReturnEmptyForUnusedMove()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var shared = (await client.GetFromJsonAsync<PokemonView[]>($"/moves/{Id(1)}/pokemon", Json))!;
        Assert.Equal(new[] { Id(201), Id(204) }, shared.Select(p => p.Id));
        var potential = (await client.GetFromJsonAsync<SpeciesView[]>($"/moves/{Id(5)}/species", Json))!;
        Assert.Equal(Id(101), Assert.Single(potential).Id);
        Assert.Empty((await client.GetFromJsonAsync<PokemonView[]>($"/moves/{Id(5)}/pokemon", Json))!);
        var created = await Body(await Send(client, HttpMethod.Post, "/moves", Input("moves")));
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>($"/moves/{created["id"]}/species"))!);
        await Problem(await client.GetAsync($"/moves/{Guid.NewGuid()}/pokemon"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReferentialIntegrityRejectsDeletesAndDestructiveLearnsetUpdates()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        await Problem(await client.DeleteAsync($"/moves/{Id(1)}"), HttpStatusCode.Conflict);
        await Problem(await client.DeleteAsync($"/species/{Id(101)}"), HttpStatusCode.Conflict);
        var species = Node("species"); species["name"] = "Charmander";
        await Problem(await Send(client, HttpMethod.Put, $"/species/{Id(101)}", species), HttpStatusCode.Conflict);
        species["learnset"]![0]!["moveId"] = Guid.NewGuid();
        await Problem(await Send(client, HttpMethod.Put, $"/species/{Id(101)}", species), HttpStatusCode.BadRequest);
        var learned = (await client.GetFromJsonAsync<PokemonView>($"/pokemon/{Id(201)}/moves", Json))!;
        Assert.Equal(4, learned.Moves.Count);
    }

    [Fact]
    public async Task CatalogEditsAreVisibleThroughStableReferences()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var response = await Send(client, HttpMethod.Put, $"/moves/{Id(1)}", new MoveInput("Updated scratch", 45, PokemonType.Normal));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var learned = (await client.GetFromJsonAsync<PokemonView>($"/pokemon/{Id(201)}/moves", Json))!;
        var move = Assert.Single(learned.Moves, m => m.Id == Id(1));
        Assert.Equal("Updated scratch", move.Name); Assert.Equal(45, move.Power);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreatesCommitExactlyOnce()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Send(client, HttpMethod.Post, "/moves", Input("moves"))));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(11, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var padded = new MoveInput("  TEST MOVE  ", 40, PokemonType.Normal);
        await Problem(await Send(client, HttpMethod.Post, "/moves", padded), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OpenApiDocumentsEveryPokedexRoute()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var spec = (await client.GetFromJsonAsync<JsonObject>("/openapi/v1.json"))!;
        foreach (var resource in new[] { "moves", "species", "pokemon" })
        {
            var collection = spec["paths"]![$"/{resource}/"] ?? spec["paths"]![$"/{resource}"];
            Assert.NotNull(collection!["get"]); Assert.NotNull(collection["post"]);
            foreach (var method in new[] { "get", "put", "delete" }) Assert.NotNull(spec["paths"]![$"/{resource}/{{id}}"]![method]);
        }
        Assert.NotNull(spec["paths"]!["/pokemon/{id}/moves"]);
        Assert.NotNull(spec["paths"]!["/pokemon/{id}/possible-moves"]);
        Assert.NotNull(spec["paths"]!["/moves/{id}/pokemon"]);
        Assert.NotNull(spec["paths"]!["/moves/{id}/species"]);
    }
    [Fact]
    public async Task RemovingReferencesAllowsDeletionWithoutLeavingOrphans()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var move = await Body(await Send(client, HttpMethod.Post, "/moves", Input("moves")));
        var moveId = (Guid)move["id"]!;
        var input = new SpeciesInput("Disposable species", PokemonType.Fire, new(40, 40, 40, 40, 40, 40), [new(moveId, 1)]);
        var species = await Body(await Send(client, HttpMethod.Post, "/species", input));
        var speciesId = (Guid)species["id"]!;
        await Problem(await client.DeleteAsync($"/moves/{moveId}"), HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Put, $"/species/{speciesId}", input with { Learnset = [] })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/moves/{moveId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/species/{speciesId}")).StatusCode);
    }

    [Fact]
    public async Task UpdatingSpeciesChangesBaseStatsButPreservesIndividualHealth()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var original = (await client.GetFromJsonAsync<SpeciesView>($"/species/{Id(101)}", Json))!;
        var input = new SpeciesInput(original.Name, original.Type, original.Stats with { Attack = 100 },
            original.Learnset.Select(e => new LearningInput(e.Move.Id, e.Level)).ToArray());
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Put, $"/species/{Id(101)}", input)).StatusCode);
        var pokemon = (await client.GetFromJsonAsync<PokemonView>($"/pokemon/{Id(201)}", Json))!;
        Assert.Equal(100, pokemon.Stats.Attack); Assert.Equal(39, pokemon.CurrentHealth); Assert.Equal(39, pokemon.TotalHealth);
    }

    [Fact]
    public async Task PokemonCanBeUsedInExistingDamageCalculatorWithoutChangingStoredHealth()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var attacker = (await client.GetFromJsonAsync<PokemonView>($"/pokemon/{Id(202)}", Json))!;
        var defender = (await client.GetFromJsonAsync<PokemonView>($"/pokemon/{Id(201)}", Json))!;
        static object Snapshot(PokemonView p) => new
        {
            p.Id, p.Name, p.Level, p.Type, p.CurrentHealth, p.TotalHealth, p.Stats.Attack, p.Stats.Defense,
            p.Stats.SpecialAttack, p.Stats.SpecialDefense, p.Stats.Speed,
            Moves = p.Moves.Select(m => new { m.Name, m.Power, m.Type })
        };
        var result = await Send(client, HttpMethod.Post, "/damage", new { Attacker = Snapshot(attacker), Defender = Snapshot(defender), MoveName = "Water Gun" });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var damage = await Body(result); Assert.Equal(17, (int)damage["damage"]!); Assert.Equal(2, (int)damage["effectiveness"]!);
        Assert.Equal(39, (await client.GetFromJsonAsync<PokemonView>($"/pokemon/{Id(201)}", Json))!.CurrentHealth);
    }

    [Theory]
    [InlineData("moves", "type", "Unknown")]
    [InlineData("moves", "name", " ")]
    [InlineData("species", "name", " ")]
    [InlineData("pokemon", "name", " ")]
    public async Task InvalidNamesAndTypesHaveValidationErrors(string resource, string property, string value)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var input = Node(resource); input[property] = value;
        await Problem(await Send(client, HttpMethod.Post, $"/{resource}", input), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScalarExamplesCanBeSubmittedToAllCreateEndpoints()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var spec = (await client.GetFromJsonAsync<JsonObject>("/openapi/v1.json"))!;
        foreach (var resource in new[] { "moves", "species", "pokemon" })
        {
            var route = spec["paths"]![$"/{resource}/"] ?? spec["paths"]![$"/{resource}"];
            var example = route!["post"]!["requestBody"]!["content"]!["application/json"]!["example"]!;
            Assert.Equal(HttpStatusCode.Created, (await Send(client, HttpMethod.Post, $"/{resource}", example)).StatusCode);
        }
    }

}
