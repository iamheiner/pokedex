using Pokemon.Application.Feature.Damage;

namespace Pokemon.Api.Feature.Damage;

/// <summary>Registra las dependencias que necesita la funcionalidad de cálculo de daño.</summary>
public static class DamageFeature
{
    public static IServiceCollection AddDamageFeature(this IServiceCollection services)
    {
        services.AddSingleton<IDamageRandom, DamageRandom>();
        return services;
    }
}
