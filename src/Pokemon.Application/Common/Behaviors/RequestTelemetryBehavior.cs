using Pokemon.Domain.Common.Exceptions;
using Pokemon.Application.Feature.Pokedex.Exceptions;
using Pokemon.Domain.Pokedex.Exceptions;
using Pokemon.Domain.Battle.Exceptions;
using Microsoft.Extensions.Logging;
using Pokemon.Application.Feature.Battle;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Pokedex;
using Pokemon.Domain.Pokedex;
using Pokemon.Application.Common.Exceptions;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using MediatR;

namespace Pokemon.Application.Common.Behaviors;

/// <summary>
/// Envuelve todos los handlers de MediatR para medir duración y resultado, y crear
/// una traza hija de la petición HTTP. Las nuevas funcionalidades heredan este comportamiento.
/// </summary>
public sealed class RequestTelemetryBehavior<TRequest, TResponse>(ILogger<RequestTelemetryBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    /// <summary>
    /// Mide la duración y el resultado del caso de uso y emite trazas, métricas y logs correlacionados.
    /// </summary>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        using var activity = RequestTelemetry.Source.StartActivity(name);
        activity?.SetTag("request.type", name);
        var started = Stopwatch.GetTimestamp();
        var outcome = "success";
        try
        {
            var response = await next();
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        catch (Exception error) when (error is InvalidDamageRequestException or PokedexRuleException or PokedexNotFoundException or PokedexConflictException or PersistenceConflictException
            or BattleRuleException or BattleNotFoundException or BattleConflictException)
        {
            outcome = "invalid";
            activity?.SetTag("request.outcome", outcome);
            throw;
        }
        catch (Exception error)
        {
            outcome = "error";
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", error.GetType().Name);
            // Se registra el tipo, sin volcar el cuerpo de la petición ni sus datos.
            logger.LogError("Error en {RequestType}: {ErrorType}", name, error.GetType().Name);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            activity?.SetTag("request.outcome", outcome);
            var tags = new TagList { { "request.type", name }, { "request.outcome", outcome } };
            RequestTelemetry.Count.Add(1, tags);
            RequestTelemetry.Duration.Record(elapsed, tags);
            // OpenTelemetry asocia este log con TraceId y SpanId de la actividad actual.
            logger.LogInformation("{RequestType} terminó con {Outcome} en {ElapsedMs} ms", name, outcome, elapsed);
        }
    }
}

public static class RequestTelemetry
{
    public const string Name = "Pokemon.Cqrs";
    internal static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);
    internal static readonly Counter<long> Count = Meter.CreateCounter<long>("pokemon.requests");
    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("pokemon.request.duration", "ms");
}
