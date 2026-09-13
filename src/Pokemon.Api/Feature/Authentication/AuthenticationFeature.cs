using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Pokemon.Api.Feature.Authentication;

/// <summary>
/// Configura la API como recurso protegido: Keycloak emite los tokens y ASP.NET verifica
/// su firma con las claves públicas del realm. Ni el dominio ni los casos de uso conocen Keycloak.
/// </summary>
public static class AuthenticationFeature
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authority = configuration["Authentication:Authority"];
        var audience = configuration["Authentication:Audience"];
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) ||
            (issuer.Scheme != "https" && issuer.Scheme != "http") || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configure Authentication:Authority and Authentication:Audience.");

        var requireHttps = configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        if (requireHttps && issuer.Scheme != "https")
            throw new InvalidOperationException("Authentication requires HTTPS. HTTP is only supported through explicit local configuration.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = authority;
            // Dentro de Docker la dirección de descubrimiento es interna, pero el emisor
            // esperado sigue siendo la URL pública del realm. No se desactiva su validación.
            options.MetadataAddress = configuration["Authentication:MetadataAddress"]
                ?? $"{authority!.TrimEnd('/')}/.well-known/openid-configuration";
            options.Audience = audience;
            options.RequireHttpsMetadata = requireHttps;
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = authority,
                ValidateAudience = true, ValidAudience = audience,
                ValidateLifetime = true, RequireExpirationTime = true,
                RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "preferred_username"
            };
        });

        // Protege también endpoints futuros, OpenAPI y Scalar sin depender de que alguien
        // recuerde añadir RequireAuthorization en cada nueva ruta. Las sondas de salud
        // declaran AllowAnonymous de forma explícita; son la única excepción.
        var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser().Build();
        services.AddAuthorizationBuilder().SetDefaultPolicy(policy).SetFallbackPolicy(policy);
        return services;
    }
}
