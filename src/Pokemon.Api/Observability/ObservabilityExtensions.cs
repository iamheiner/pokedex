using Npgsql;
using MediatR;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Pokemon.Api.Observability;

/// <summary>Configura trazas, métricas y logs; el dominio no depende del destino de telemetría.</summary>
public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var export = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestTelemetryBehavior<,>));
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "pokemon-api"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(RequestTelemetry.Name)
                    .AddNpgsql();
                if (export) tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(RequestTelemetry.Name);
                if (export) metrics.AddOtlpExporter();
            });
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "pokemon-api"));
            if (export) logging.AddOtlpExporter();
        });
        // Sin endpoint OTLP puede ejecutarse localmente sin un collector obligatorio.
        return builder;
    }
}
