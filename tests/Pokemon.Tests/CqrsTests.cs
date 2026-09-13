using Pokemon.Domain.Common.Persistence;
using Pokemon.Tests.Persistence;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Pokedex;
using Pokemon.Application.Common.Exceptions;
using System.Diagnostics;
using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Pokemon.Api.Observability;
using Pokemon.Application;
using Pokemon.Application.Feature.Damage;
using Pokemon.Application.Feature.Damage.Queries.CalculateDamage;
using Pokemon.Domain;

namespace Pokemon.Tests;

public sealed class CqrsTests
{
    /// <summary>Comprueba que MediatR resuelve el handler y emite una traza vinculada a la petición original.</summary>
    [Theory]
    [InlineData("Flame", "success")]
    [InlineData("Unknown", "invalid")]
    public async Task SenderResolvesHandlerAndEmitsCorrelatedTrace(string moveName, string outcome)
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Pokemon.Cqrs",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = new Activity("HTTP request").Start();
        var random = new CountingRandom();
        using var provider = Build(random);
        var sender = provider.GetRequiredService<ISender>();
        var query = Query(moveName);
        if (outcome == "success")
        {
            var result = await sender.Send(query);
            Assert.Equal(74, result.Damage);
            Assert.Equal(1, random.Calls);
            Assert.Equal(100, query.Defender.CurrentHealth);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDamageRequestException>(() => sender.Send(query));
            Assert.Equal(0, random.Calls);
        }
        var span = Assert.Single(activities, activity => activity.ParentSpanId == parent.SpanId);
        Assert.Equal(parent.TraceId, span.TraceId);
        Assert.Equal("CalculateDamageQuery", span.DisplayName);
        Assert.Equal(outcome, span.GetTagItem("request.outcome"));
    }

    /// <summary>Comprueba que una consulta cancelada no ejecuta el cálculo ni consume azar.</summary>
    [Fact]
    public async Task CancelledQueryDoesNotCalculateDamage()
    {
        var random = new CountingRandom();
        using var provider = Build(random);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<ISender>().Send(Query("Flame"), cancellation.Token));
        Assert.Equal(0, random.Calls);
    }

    /// <summary>Comprueba que el pipeline propaga los fallos inesperados del caso de uso.</summary>
    [Fact]
    public async Task UnexpectedFailureIsPropagatedThroughPipeline()
    {
        using var provider = Build(new BrokenRandom());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<ISender>().Send(Query("Flame")));
    }

    /// <summary>Construye el contenedor de pruebas con MediatR, persistencia simulada y azar controlado.</summary>
    private static ServiceProvider Build(IDamageRandom random)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IUnitOfWork>(_ => new InMemoryUnitOfWork(seed: false));
        services.AddSingleton<IReadSession>(provider => (IReadSession)provider.GetRequiredService<IUnitOfWork>());
        services.AddSingleton(random);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Construye una consulta de daño con participantes conocidos y el nombre de movimiento indicado.</summary>
    private static CalculateDamageQuery Query(string moveName)
    {
        var move = new Move("Flame", 100, PokemonType.Fire);
        var attacker = new Combatant(Guid.NewGuid(), "Attacker", 50, PokemonType.Water,
            100, 100, 100, 100, 100, 100, 100, [move]);
        var defender = new Combatant(Guid.NewGuid(), "Defender", 50, PokemonType.Grass,
            100, 100, 100, 100, 100, 100, 100, []);
        return new CalculateDamageQuery(attacker, moveName, defender);
    }

    private sealed class CountingRandom : IDamageRandom
    {
        public int Calls { get; private set; }
        /// <summary>Cuenta la solicitud de azar y devuelve el factor fijo 85 para la prueba.</summary>
        public int Next() { Calls++; return 85; }
    }

    private sealed class BrokenRandom : IDamageRandom
    {
        /// <summary>Simula un fallo inesperado al solicitar un factor aleatorio.</summary>
        public int Next() => throw new InvalidOperationException("Test failure");
    }
}
