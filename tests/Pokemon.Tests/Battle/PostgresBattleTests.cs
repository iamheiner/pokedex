using Pokemon.Domain.Battle.Exceptions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Battle;
using Pokemon.Application.Feature.Battle.Contracts;
using Pokemon.Infrastructure.Persistence;
using Pokemon.Infrastructure.Persistence.Repositories;
using Pokemon.Infrastructure.Persistence.Migrations;
using Pokemon.Infrastructure.Persistence.Serialization;
namespace Pokemon.Tests.Battle;

/// <summary>Suite explícita contra PostgreSQL real; cada caso crea y elimina su propio esquema aislado.</summary>
[Trait("Category", "Postgres")]
public sealed class PostgresBattleTests
{
    /// <summary>Comprueba que las migraciones pueden repetirse y ejecutarse desde conexiones concurrentes sin duplicarse.</summary>
    [PostgresFact]
    public async Task MigrationIsIdempotentAndSafeAcrossConcurrentConnections()
    {
        await using var db = await TestDatabase.Create(migrate: false);
        await using var second = NpgsqlDataSource.Create(db.ConnectionString);
        await Task.WhenAll(new BattleDatabaseMigrator(new DatabaseConnectionFactory(db.Source)).Migrate(default), new BattleDatabaseMigrator(new DatabaseConnectionFactory(second)).Migrate(default));
        await new BattleDatabaseMigrator(new DatabaseConnectionFactory(db.Source)).Migrate(default);
        await using var command = db.Source.CreateCommand("SELECT count(*) FROM battle_schema_migrations WHERE version = 1");
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        await using var constraint = db.Source.CreateCommand("SELECT count(*) FROM pg_constraint WHERE conrelid = 'battles'::regclass");
        Assert.Equal(5L, await constraint.ExecuteScalarAsync());
    }

    /// <summary>Comprueba que una nueva conexión recupera y continúa exactamente el estado de partida guardado.</summary>
    [PostgresFact]
    public async Task NewDataSourceRecoversAndContinuesTheExactSavedState()
    {
        await using var db = await TestDatabase.Create();
        var battle = BattleDomainTests.Duel(19, 19);
        for (var n = 0; n < 43; n++) battle = BattleDomainTests.Next(battle);
        await new BattleTestClient(new DatabaseConnectionFactory(db.Source)).Add(battle, default);
        await using var reopened = NpgsqlDataSource.Create(db.ConnectionString);
        var store = new BattleTestClient(new DatabaseConnectionFactory(reopened));
        var restored = await store.Get(battle.Id, default);
        Assert.Equal(JsonSerializer.Serialize(battle), JsonSerializer.Serialize(restored));
        var next = await store.Update(battle.Id, BattleDomainTests.Next, default);
        Assert.Equal(battle.Version + 1, next.Version);
        Assert.Equal(15, next.First.Snapshot.CurrentHealth); // Cuatro esfuerzos de un punto.
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(await store.Get(next.Id, default)));
    }

    /// <summary>Comprueba que distintas instancias aceptan un único turno concurrente y consumen una sola muestra aleatoria.</summary>
    [PostgresFact]
    public async Task IndependentStoresAcceptOneConcurrentTurnAndOneRandomDraw()
    {
        await using var db = await TestDatabase.Create();
        await using var otherSource = NpgsqlDataSource.Create(db.ConnectionString);
        var stores = new[] { new BattleTestClient(new DatabaseConnectionFactory(db.Source)), new BattleTestClient(new DatabaseConnectionFactory(otherSource)) };
        var battle = BattleDomainTests.Duel(); await stores[0].Add(battle, default);
        var draws = 0;
        var attempts = Enumerable.Range(0, 12).Select(async index =>
        {
            try
            {
                await stores[index % 2].Update(battle.Id, current =>
                {
                    current.EnsureCanAct(battle.First.Snapshot.Id, battle.First.Moves[0].Id, battle.Version);
                    Interlocked.Increment(ref draws);
                    return current.PlayTurn(battle.First.Snapshot.Id, battle.First.Moves[0].Id, battle.Version, 100);
                }, default);
                return true;
            }
            catch (BattleConflictException) { return false; }
        });
        var results = await Task.WhenAll(attempts);
        Assert.Equal(1, results.Count(success => success)); Assert.Equal(1, draws);
        Assert.Single((await stores[0].Get(battle.Id, default)).Turns);
    }

    /// <summary>Comprueba que excepciones y cancelaciones revierten los cambios sobre la fila de partida.</summary>
    [PostgresFact]
    public async Task ExceptionAndCancellationRollBackWithoutChangingTheRow()
    {
        await using var db = await TestDatabase.Create(); var store = new BattleTestClient(new DatabaseConnectionFactory(db.Source));
        var battle = BattleDomainTests.Duel(); await store.Add(battle, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Update(battle.Id, _ => throw new InvalidOperationException(), default));
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Update(battle.Id, current =>
        { var next = BattleDomainTests.Next(current); cancellation.Cancel(); return next; }, cancellation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Update(battle.Id, current => current, default));
        Assert.Equal(1, (await store.Get(battle.Id, default)).Version);
        await Assert.ThrowsAsync<BattleConflictException>(() => store.Add(battle, default));
        await Assert.ThrowsAsync<BattleNotFoundException>(() => store.Get(Guid.NewGuid(), default));
    }

    /// <summary>Comprueba que bloquear una partida no bloquea otra y que cancelar la espera no guarda ningún turno.</summary>
    [PostgresFact]
    public async Task LockingOneBattleDoesNotBlockAnotherAndCancelledWaitLeavesNoTurn()
    {
        await using var db = await TestDatabase.Create(); var store = new BattleTestClient(new DatabaseConnectionFactory(db.Source));
        var first = BattleDomainTests.Duel(); var second = BattleDomainTests.Duel();
        await store.Add(first, default); await store.Add(second, default);
        await using var connection = await db.Source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM battles WHERE id = $1 FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue(first.Id); await command.ExecuteScalarAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Update(first.Id, BattleDomainTests.Next, timeout.Token));
        var updated = await store.Update(second.Id, BattleDomainTests.Next, default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, updated.Version);
        await transaction.RollbackAsync();
        Assert.Equal(1, (await store.Get(first.Id, default)).Version);
    }

    /// <summary>Comprueba que las restricciones y el lector rechazan documentos corruptos y versiones nulas.</summary>
    [PostgresFact]
    public async Task CorruptDocumentsAndNullVersionsAreRejected()
    {
        await using var db = await TestDatabase.Create(); var store = new BattleTestClient(new DatabaseConnectionFactory(db.Source));
        var battle = BattleDomainTests.Next(BattleDomainTests.Duel()); await store.Add(battle, default);
        await using var invalid = db.Source.CreateCommand("UPDATE battles SET document = jsonb_set(document, '{version}', 'null') WHERE id = $1");
        invalid.Parameters.AddWithValue(battle.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        await using var corrupt = db.Source.CreateCommand("UPDATE battles SET document = jsonb_set(document, '{turns,0,appliedDamage}', '99') WHERE id = $1");
        corrupt.Parameters.AddWithValue(battle.Id); await corrupt.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.Get(battle.Id, default));
    }

    /// <summary>Comprueba que una partida terminada conserva su resultado y rechaza nuevas acciones al reconectar.</summary>
    [PostgresFact]
    public async Task FinishedBattleSurvivesReconnectionAndStillRejectsActions()
    {
        await using var db = await TestDatabase.Create(); var store = new BattleTestClient(new DatabaseConnectionFactory(db.Source));
        var battle = BattleDomainTests.Duel(1, 1);
        while (battle.Phase != BattlePhase.Finished) battle = BattleDomainTests.Next(battle);
        await store.Add(battle, default);
        await using var reopened = NpgsqlDataSource.Create(db.ConnectionString);
        var restored = await new BattleTestClient(new DatabaseConnectionFactory(reopened)).Get(battle.Id, default);
        Assert.True(restored.IsDraw); Assert.Equal(BattlePhase.Finished, restored.Phase);
        await Assert.ThrowsAsync<BattleConflictException>(() => store.Update(battle.Id,
            b => b.PlayTurn(b.First.Snapshot.Id, null, b.Version, 100), default));
    }

    /// <summary>Comprueba que una nueva instancia de la API recupera y continúa una partida persistida.</summary>
    [PostgresFact]
    public async Task FreshApiHostReadsExistingBattleAndContinuesIt()
    {
        await using var db = await TestDatabase.Create();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        BattleView saved;
        using (var firstHost = new DatabaseApiFactory(db.ConnectionString))
        using (var client = firstHost.CreateClient())
        {
            var created = await client.PostAsJsonAsync("/battles", new CreateBattleInput(
                Guid.Parse("00000000-0000-0000-0000-000000000201"), Guid.Parse("00000000-0000-0000-0000-000000000202")));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var battle = (await created.Content.ReadFromJsonAsync<BattleView>(json))!;
            var played = await client.PostAsJsonAsync($"/battles/{battle.Id}/turns", new PlayTurnInput(battle.First.Id, battle.First.Moves[0].Id, battle.Version));
            Assert.Equal(HttpStatusCode.OK, played.StatusCode);
            saved = (await played.Content.ReadFromJsonAsync<BattleView>(json))!;
        }
        using var secondHost = new DatabaseApiFactory(db.ConnectionString); using var recoveredClient = secondHost.CreateClient();
        var recovered = (await recoveredClient.GetFromJsonAsync<BattleView>($"/battles/{saved.Id}", json))!;
        Assert.Equal(JsonSerializer.Serialize(saved, json), JsonSerializer.Serialize(recovered, json));
        Assert.Equal(HttpStatusCode.OK, (await recoveredClient.GetAsync("/health/ready")).StatusCode);
        var next = await recoveredClient.PostAsJsonAsync($"/battles/{saved.Id}/turns", new PlayTurnInput(recovered.Second.Id, recovered.Second.Moves[0].Id, recovered.Version));
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(saved.Version + 1, (await next.Content.ReadFromJsonAsync<BattleView>(json))!.Version);
    }

    private sealed class DatabaseApiFactory(string connection) : WebApplicationFactory<Program>
    {
        /// <summary>Configura el cliente del host PostgreSQL de pruebas con un token válido.</summary>
        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            Authentication.TestTokens.Authorize(client);
        }

        /// <summary>Configura el host HTTP de pruebas para utilizar PostgreSQL y los metadatos de autenticación locales.</summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            Authentication.TestTokens.Configure(builder);
            builder.UseSetting("ConnectionStrings:Battles", connection);
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "");
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    /// <summary>Omite explícitamente la prueba PostgreSQL cuando no se ha configurado su conexión.</summary>
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POKEMON_POSTGRES_TEST_CONNECTION")))
            Skip = "Run docker compose run --build --rm postgres-tests for real PostgreSQL tests.";
    }
}

internal sealed class TestDatabase : IAsyncDisposable
{
    public NpgsqlDataSource Source { get; }
    public string ConnectionString { get; }
    private readonly NpgsqlDataSource admin;
    private readonly string schema;
    /// <summary>Conserva el esquema temporal y crea su origen de conexiones para las pruebas PostgreSQL.</summary>
    private TestDatabase(NpgsqlDataSource admin, string connection, string schema)
    { this.admin = admin; this.schema = schema; ConnectionString = connection; Source = NpgsqlDataSource.Create(connection); }

    /// <summary>Crea un esquema PostgreSQL aislado y aplica opcionalmente las migraciones de partidas.</summary>
    public static async Task<TestDatabase> Create(bool migrate = true)
    {
        var connection = Environment.GetEnvironmentVariable("POKEMON_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Missing PostgreSQL test connection.");
        var admin = NpgsqlDataSource.Create(connection);
        var schema = "battle_tests_" + Guid.NewGuid().ToString("N");
        await using var create = admin.CreateCommand($"CREATE SCHEMA {schema}"); await create.ExecuteNonQueryAsync();
        var settings = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema };
        var database = new TestDatabase(admin, settings.ConnectionString, schema);
        try
        {
            if (migrate) await new BattleDatabaseMigrator(new DatabaseConnectionFactory(database.Source)).Migrate(default);
            return database;
        }
        catch { await database.DisposeAsync(); throw; }
    }
    /// <summary>Libera las conexiones y elimina únicamente el esquema temporal generado por esta prueba.</summary>
    public async ValueTask DisposeAsync()
    {
        await Source.DisposeAsync();
        // Solo se elimina el esquema temporal generado por esta instancia, nunca public ni datos de la API.
        if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^battle_tests_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Unsafe test schema.");
        await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE"); await drop.ExecuteNonQueryAsync();
        await admin.DisposeAsync();
    }
}
