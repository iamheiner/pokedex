using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Pokemon.Tests.Authentication;

/// <summary>
/// Sustituye únicamente el descubrimiento remoto por una clave efímera de pruebas.
/// Las peticiones siguen atravesando el validador JWT real y la política de producción.
/// </summary>
internal static class TestTokens
{
    public const string Issuer = "https://identity.test/realms/pokemon";
    private static readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "tests" };

    /// <summary>
    /// Configura metadatos y claves locales para validar tokens y OIDC sin contactar con un servidor externo.
    /// </summary>
    public static void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Authority", Issuer);
        builder.UseSetting("Authentication:DocumentationClientSecret", "test-only");
        builder.UseSetting("Authentication:Audience", "pokemon-api");
        builder.UseSetting("Authentication:RequireHttpsMetadata", "true");
        builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
            metadata.SigningKeys.Add(Key);
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
        }));
        builder.ConfigureServices(services => services.PostConfigure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>(
            Pokemon.Api.Feature.Authentication.DocumentationAuthentication.Oidc, options =>
            {
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new()
                {
                    Issuer = Issuer, AuthorizationEndpoint = Issuer + "/protocol/openid-connect/auth",
                    TokenEndpoint = Issuer + "/protocol/openid-connect/token"
                });
            }));
    }

    /// <summary>
    /// Genera un JWT de prueba con parámetros que permiten simular credenciales válidas e inválidas.
    /// </summary>
    public static string Create(string issuer = Issuer, string audience = "pokemon-api", bool expired = false, bool wrongKey = false, bool unsigned = false, bool noExpiration = false, bool future = false, string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var now = DateTime.UtcNow;
        var key = wrongKey ? new RsaSecurityKey(RSA.Create(2048)) { KeyId = "wrong" } : Key;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer, audience, [new Claim("sub", "test-user")],
            future ? now.AddMinutes(2) : now.AddMinutes(-10), noExpiration ? null : expired ? now.AddMinutes(-1) : now.AddMinutes(5),
            unsigned ? null : new SigningCredentials(key, algorithm)));
    }

    /// <summary>
    /// Añade un token Bearer válido a las cabeceras del cliente HTTP de pruebas.
    /// </summary>
    public static void Authorize(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Create());
}
