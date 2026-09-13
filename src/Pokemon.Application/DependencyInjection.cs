using MediatR;
using Pokemon.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace Pokemon.Application;

/// <summary>Registra MediatR y los handlers de las funcionalidades de Application.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Se explora el ensamblado completo una vez: los nuevos handlers de otras
        // funcionalidades se registrarán sin añadirlos individualmente a Program.cs.
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestTelemetryBehavior<,>));
        return services;
    }
}
