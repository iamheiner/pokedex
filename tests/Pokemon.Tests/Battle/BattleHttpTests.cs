using Pokemon.Domain.Common.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Application.Feature.Damage;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Battle;
namespace Pokemon.Tests.Battle;

public sealed class BattleHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    /// <summary>
    /// Genera el identificador estable de un ejemplar del catálogo utilizado por la prueba.
    /// </summary>
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");
    /// <summary>
    /// Crea una partida por HTTP y valida el estado de creación y la cabecera Location.
    /// </summary>
    private static async Task<BattleView> Create(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/battles", new CreateBattleInput(Id(201), Id(202)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var battle = (await response.Content.ReadFromJsonAsync<BattleView>(Json))!;
        Assert.Equal($"/battles/{battle.Id}", response.Headers.Location!.ToString());
        return battle;
    }
    /// <summary>
    /// Envía por HTTP la siguiente acción válida de la partida.
    /// </summary>
    private static Task<HttpResponseMessage> Act(HttpClient client, BattleView battle) =>
        client.PostAsJsonAsync($"/battles/{battle.Id}/turns", Input(battle));
    /// <summary>
    /// Construye la acción del siguiente actor seleccionando un movimiento disponible o esfuerzo.
    /// </summary>
    private static PlayTurnInput Input(BattleView battle)
    {
        var actor = battle.NextPokemonId == battle.First.Id ? battle.First : battle.Second;
        return new(actor.Id, actor.Moves.FirstOrDefault(m => m.RemainingUses > 0)?.Id, battle.Version);
    }
    /// <summary>
    /// Valida el estado y el contenido ProblemDetails de una respuesta HTTP de combate.
    /// </summary>
    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((int)status, (int)problem["status"]!); Assert.False(string.IsNullOrWhiteSpace((string?)problem["traceId"]));
    }
    /// <summary>
    /// Comprueba por HTTP que cada turno queda guardado hasta que termina la partida por salud cero.
    /// </summary>
    [Fact]
    public async Task FullBattlePersistsEveryTurnUntilHealthReachesZero()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var before = await client.GetStringAsync($"/pokemon/{Id(201)}");
        var battle = await Create(client);
        Assert.Equal(Id(201), battle.NextPokemonId); Assert.Equal(1, battle.Version); Assert.Empty(battle.Turns);
        while (battle.Phase == BattlePhase.AwaitingAction && battle.Turns.Count < 60)
        {
            var response = await Act(client, battle); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var next = (await response.Content.ReadFromJsonAsync<BattleView>(Json))!;
            Assert.Equal(battle.Version + 1, next.Version); Assert.Equal(battle.Turns.Count + 1, next.Turns.Count);
            Assert.Equal(battle.NextPokemonId, next.Turns[^1].AttackerId);
            battle = next;
        }
        Assert.Equal(BattlePhase.Finished, battle.Phase); Assert.Null(battle.NextPokemonId);
        Assert.True(battle.First.CurrentHealth == 0 || battle.Second.CurrentHealth == 0);
        Assert.True(battle.IsDraw || battle.WinnerId.HasValue);
        var saved = (await client.GetFromJsonAsync<BattleView>($"/battles/{battle.Id}", Json))!;
        Assert.Equal(JsonSerializer.Serialize(battle, Json), JsonSerializer.Serialize(saved, Json));
        await Problem(await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", new PlayTurnInput(battle.First.Id, battle.First.Moves[0].Id, battle.Version)), HttpStatusCode.Conflict);
        Assert.Equal(before, await client.GetStringAsync($"/pokemon/{Id(201)}"));
    }
    /// <summary>
    /// Comprueba que la versión y el actor impiden ataques duplicados o fuera de turno.
    /// </summary>
    [Fact]
    public async Task VersionAndActorRejectDuplicateOrOutOfTurnAttacks()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(); var battle = await Create(client);
        var input = new PlayTurnInput(battle.First.Id, Id(2), 1);
        var response = await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<BattleView>(Json))!;
        Assert.Equal(3, updated.Turns[0].AppliedDamage); Assert.Equal(41, updated.Second.CurrentHealth);
        Assert.Equal(4, updated.First.Moves.Single(m => m.Id == Id(2)).RemainingUses);
        await Problem(await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", input), HttpStatusCode.Conflict);
        await Problem(await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", input with { ExpectedVersion = 2 }), HttpStatusCode.Conflict);
        Assert.Equal(2, (await client.GetFromJsonAsync<BattleView>($"/battles/{battle.Id}", Json))!.Version);
    }
    /// <summary>
    /// Comprueba que turnos HTTP idénticos concurrentes producen un solo golpe y una sola muestra aleatoria.
    /// </summary>
    [Fact]
    public async Task ConcurrentIdenticalTurnsProduceOneHitAndOneRandomSample()
    {
        var random = new CountingRandom(); using var factory = new ApiFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        { services.RemoveAll<IDamageRandom>(); services.AddSingleton<IDamageRandom>(random); }));
        using var client = app.CreateClient(); var battle = await Create(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Act(client, battle)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(11, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict)); Assert.Equal(1, random.Calls);
        var saved = (await client.GetFromJsonAsync<BattleView>($"/battles/{battle.Id}", Json))!;
        Assert.Single(saved.Turns); Assert.Equal(2, saved.Version);
    }
    /// <summary>
    /// Comprueba que editar o eliminar datos del catálogo no cambia una partida ya creada.
    /// </summary>
    [Fact]
    public async Task CatalogChangesAndDeletionDoNotChangeAnExistingBattle()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(); var battle = await Create(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/moves/{Id(1)}", new { name = "Changed", power = 250, type = "Fire" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/pokemon/{Id(201)}")).StatusCode);
        var saved = (await client.GetFromJsonAsync<BattleView>($"/battles/{battle.Id}", Json))!;
        Assert.Equal("Scratch", saved.First.Moves[0].Name); Assert.Equal(40, saved.First.Moves[0].Power);
        Assert.Equal(HttpStatusCode.OK, (await Act(client, battle)).StatusCode);
        await Problem(await client.GetAsync($"/pokemon/{Id(201)}"), HttpStatusCode.NotFound);
    }
    /// <summary>
    /// Comprueba que dos partidas con los mismos ejemplares mantienen salud y usos independientes.
    /// </summary>
    [Fact]
    public async Task TwoBattlesWithTheSamePokemonHaveIndependentHealthAndUses()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var first = await Create(client); var second = await Create(client);
        Assert.NotEqual(first.Id, second.Id); Assert.Equal(HttpStatusCode.OK, (await Act(client, first)).StatusCode);
        var untouched = (await client.GetFromJsonAsync<BattleView>($"/battles/{second.Id}", Json))!;
        Assert.Equal(1, untouched.Version); Assert.Equal(44, untouched.Second.CurrentHealth); Assert.All(untouched.First.Moves, m => Assert.Equal(5, m.RemainingUses));
    }
    /// <summary>
    /// Comprueba que crear una partida requiere enviar las dos identidades de participantes.
    /// </summary>
    [Theory]
    [InlineData("firstPokemonId")]
    [InlineData("secondPokemonId")]
    public async Task CreationRequiresBothFields(string missing)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var input = JsonSerializer.SerializeToNode(new CreateBattleInput(Id(201), Id(202)), Json)!.AsObject(); input.Remove(missing);
        await Problem(await client.PostAsJsonAsync("/battles", input), HttpStatusCode.BadRequest);
    }
    /// <summary>
    /// Comprueba que una acción requiere todos sus campos, incluido el identificador de movimiento anulable.
    /// </summary>
    [Theory]
    [InlineData("pokemonId")]
    [InlineData("moveId")]
    [InlineData("expectedVersion")]
    public async Task TurnRequiresEveryFieldEvenNullableMoveId(string missing)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(); var battle = await Create(client);
        var input = JsonSerializer.SerializeToNode(Input(battle), Json)!.AsObject(); input.Remove(missing);
        await Problem(await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", input), HttpStatusCode.BadRequest);
    }
    /// <summary>
    /// Comprueba que participantes inválidos o sin salud no pueden iniciar una partida por HTTP.
    /// </summary>
    [Fact]
    public async Task InvalidParticipantsAndFaintedPokemonCannotStart()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        await Problem(await client.PostAsJsonAsync("/battles", new CreateBattleInput(Id(201), Id(201))), HttpStatusCode.BadRequest);
        await Problem(await client.PostAsJsonAsync("/battles", new CreateBattleInput(Guid.Empty, Id(202))), HttpStatusCode.BadRequest);
        await Problem(await client.PostAsJsonAsync("/battles", new CreateBattleInput(Guid.NewGuid(), Id(202))), HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/pokemon/{Id(201)}", new
        { speciesId = Id(101), name = "Fainted", level = 20, currentHealth = 0, totalHealth = 39, moveIds = new[] { Id(1), Id(2), Id(3), Id(4) } })).StatusCode);
        await Problem(await client.PostAsJsonAsync("/battles", new CreateBattleInput(Id(201), Id(202))), HttpStatusCode.BadRequest);
    }
    /// <summary>
    /// Comprueba que movimientos desconocidos, esfuerzo prematuro y versiones inválidas no avanzan la partida.
    /// </summary>
    [Fact]
    public async Task UnknownMovePrematureStruggleAndInvalidVersionDoNotAdvance()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(); var battle = await Create(client);
        await Problem(await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", Input(battle) with { MoveId = Guid.NewGuid() }), HttpStatusCode.Conflict);
        await Problem(await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", Input(battle) with { MoveId = null }), HttpStatusCode.Conflict);
        await Problem(await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", Input(battle) with { ExpectedVersion = 0 }), HttpStatusCode.BadRequest);
        await Problem(await client.GetAsync($"/battles/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
        await Problem(await client.PostAsJsonAsync($"/battles/{Guid.NewGuid()}/turns", Input(battle)), HttpStatusCode.NotFound);
        Assert.Equal(1, (await client.GetFromJsonAsync<BattleView>($"/battles/{battle.Id}", Json))!.Version);
    }
    /// <summary>
    /// Comprueba que un fallo del azar revierte el turno y devuelve un error interno sin detalles sensibles.
    /// </summary>
    [Fact]
    public async Task InternalRandomFailureReturnsSanitized500AndRollsBack()
    {
        using var factory = new ApiFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        { services.RemoveAll<IDamageRandom>(); services.AddSingleton<IDamageRandom>(new FixedRandom(101)); }));
        using var client = app.CreateClient(); var battle = await Create(client);
        var response = await Act(client, battle); await Problem(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain("ArgumentOutOfRange", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, (await client.GetFromJsonAsync<BattleView>($"/battles/{battle.Id}", Json))!.Version);
    }
    /// <summary>
    /// Comprueba que el ejemplo OpenAPI crea una partida y que su fase se representa como texto.
    /// </summary>
    [Fact]
    public async Task OpenApiCreationExampleIsExecutableAndPhaseIsAString()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var spec = (await client.GetFromJsonAsync<JsonObject>("/openapi/v1.json"))!;
        var create = spec["paths"]!["/battles/"] ?? spec["paths"]!["/battles"];
        var example = create!["post"]!["requestBody"]!["content"]!["application/json"]!["example"];
        var response = await client.PostAsJsonAsync("/battles", example); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!; Assert.Equal("AwaitingAction", (string?)body["phase"]);
        Assert.NotNull(spec["paths"]!["/battles/{id}/turns"]!["post"]);
    }
    /// <summary>
    /// Comprueba que agotar los movimientos habilita esfuerzo sin solicitar nuevas muestras aleatorias.
    /// </summary>
    [Fact]
    public async Task ExhaustedMovesEnableNullMoveOverHttpAndDoNotDrawMoreRandomNumbers()
    {
        var random = new CountingRandom(); using var factory = new ApiFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        { services.RemoveAll<IDamageRandom>(); services.AddSingleton<IDamageRandom>(random); }));
        using var client = app.CreateClient();
        var aggregate = BattleDomainTests.Duel(1, 1);
        await app.Services.GetRequiredService<IUnitOfWork>().WriteAsync(async scope => { await scope.GetRepository<IBattleRepository>().Add(aggregate, default); return true; }, default);
        var battle = (await client.GetFromJsonAsync<BattleView>($"/battles/{aggregate.Id}", Json))!;
        for (var turn = 0; turn < 40; turn++)
        {
            var response = await Act(client, battle); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            battle = (await response.Content.ReadFromJsonAsync<BattleView>(Json))!;
        }
        Assert.True(battle.First.CanStruggle); Assert.True(battle.Second.CanStruggle);
        Assert.Null(Input(battle).MoveId);
        var finish = await Act(client, battle); Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
        battle = (await finish.Content.ReadFromJsonAsync<BattleView>(Json))!;
        Assert.Equal(BattlePhase.Finished, battle.Phase); Assert.True(battle.IsDraw);
        Assert.True(battle.Turns[^1].IsStruggle); Assert.Equal(40, random.Calls);
        Assert.Null(battle.Turns[^1].RandomFactor);
    }

    private sealed class CountingRandom : IDamageRandom
    {
        public int Calls;
        /// <summary>
        /// Cuenta de forma segura las solicitudes de azar y devuelve el factor fijo 100.
        /// </summary>
        public int Next() { Interlocked.Increment(ref Calls); return 100; }
    }
}
