using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;

namespace Pokemon.Api.Feature.Authentication;

/// <summary>
/// La documentación usa login OIDC para poder abrirse en un navegador.
/// Su cookie nunca autoriza los endpoints de negocio: estos solo aceptan Bearer.
/// </summary>
public static class DocumentationAuthentication
{
    public const string Policy = "Documentation";
    public const string Cookie = "DocumentationCookie";
    public const string Oidc = "Keycloak";

    /// <summary>
    /// Determina si la documentación está habilitada según el entorno y la configuración.
    /// </summary>
    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() || configuration.GetValue<bool>("ApiDocumentation:Enabled");

    /// <summary>
    /// Configura el inicio de sesión OIDC y la cookie utilizada para acceder a la documentación.
    /// </summary>
    public static IServiceCollection AddDocumentationAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Authentication:DocumentationClientSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Configure Authentication:DocumentationClientSecret when documentation is enabled.");
        var requireHttps = configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        services.AddAuthentication()
            .AddPolicyScheme(Policy, Policy, options => options.ForwardDefaultSelector = context =>
                context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme : Cookie)
            .AddCookie(Cookie, options =>
            {
                options.Cookie.Name = "pokemon.documentation";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(4);
                options.SlidingExpiration = false;
                options.ForwardChallenge = Oidc;
            })
            .AddOpenIdConnect(Oidc, options =>
            {
                options.SignInScheme = Cookie;
                options.Authority = configuration["Authentication:Authority"];
                options.MetadataAddress = configuration["Authentication:MetadataAddress"]
                    ?? $"{options.Authority!.TrimEnd('/')}/.well-known/openid-configuration";
                options.RequireHttpsMetadata = requireHttps;
                options.ClientId = "pokemon-documentation";
                options.ClientSecret = secret;
                options.ResponseType = "code";
                options.ResponseMode = "query";
                options.UsePkce = true;
                options.MapInboundClaims = false;
                options.SaveTokens = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.CallbackPath = "/signin-oidc";
                // El callback GET permite SameSite=Lax también en el entorno HTTP local.
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                options.CorrelationCookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                options.Events.OnRemoteFailure = async context =>
                {
                    context.HandleResponse();
                    await Results.Problem(statusCode: 400, title: "Authentication failed",
                        detail: "The login response could not be validated. Start the login again.")
                        .ExecuteAsync(context.HttpContext);
                };
                options.Events.OnTicketReceived = context =>
                {
                    // La sesión de documentación no debe durar más que su access token.
                    var expiresAt = context.Properties?.GetTokenValue("expires_at");
                    if (DateTimeOffset.TryParse(expiresAt, out var expires))
                        context.Properties!.ExpiresUtc = expires.AddSeconds(-30);
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorizationBuilder().AddPolicy(Policy, new AuthorizationPolicyBuilder(Policy).RequireAuthenticatedUser().Build());
        return services;
    }

    /// <summary>
    /// Publica OpenAPI y Scalar con su política de acceso y configura el inicio y cierre de sesión documental.
    /// </summary>
    public static void MapProtectedDocumentation(this WebApplication app)
    {
        var group = app.MapGroup("").RequireAuthorization(Policy);
        group.MapOpenApi();
        group.MapScalarApiReference(async (options, context) =>
        {
            options.WithTitle("Pokémon API").AddPreferredSecuritySchemes("Bearer");
            var token = await context.GetTokenAsync(Cookie, "access_token");
            if (token is not null)
                options.AddHttpAuthentication("Bearer", scheme => scheme.Token = token);
        });
        // Scalar marca sus recursos estáticos como AllowAnonymous. La convención final
        // del grupo elimina esa excepción después de construir todas sus rutas.
        ((IEndpointConventionBuilder)group).Finally(endpoint =>
        {
            foreach (var anonymous in endpoint.Metadata.OfType<IAllowAnonymous>().ToArray())
                endpoint.Metadata.Remove(anonymous);
        });
    }
}
