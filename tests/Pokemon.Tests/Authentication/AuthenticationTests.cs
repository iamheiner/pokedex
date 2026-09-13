using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Pokemon.Tests.Authentication;

public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Every_mapped_endpoint_rejects_anonymous_requests_before_executing()
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = null;
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var path = Regex.Replace(endpoint.RoutePattern.RawText!, @"\{[^}]+\}", "00000000-0000-0000-0000-000000000201");
            // Las sondas de salud son la única excepción anónima: un orquestador no puede enviar Bearer.
            var probe = path is "/health" or "/health/ready";
            Assert.True((endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) == probe, endpoint.RoutePattern.RawText);
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            foreach (var method in methods)
            {
                using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
                if (probe)
                {
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    continue;
                }
                var documentation = path.StartsWith("/scalar") || path.StartsWith("/openapi");
                Assert.True(response.StatusCode == (documentation ? HttpStatusCode.Redirect : HttpStatusCode.Unauthorized), $"{method} {path}: {response.StatusCode}");
                if (documentation)
                    Assert.StartsWith(TestTokens.Issuer, response.Headers.Location!.AbsoluteUri);
                else
                    Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
            }
        }
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("malformed")]
    [InlineData("unsigned")]
    [InlineData("noExpiration")]
    [InlineData("future")]
    [InlineData("algorithm")]
    public async Task Invalid_tokens_are_rejected(string scenario)
    {
        using var client = factory.CreateClient();
        var token = scenario switch
        {
            "expired" => TestTokens.Create(expired: true),
            "issuer" => TestTokens.Create(issuer: "https://other.test"),
            "audience" => TestTokens.Create(audience: "other-api"),
            "signature" => TestTokens.Create(wrongKey: true),
            "unsigned" => TestTokens.Create(unsigned: true),
            "noExpiration" => TestTokens.Create(noExpiration: true),
            "future" => TestTokens.Create(future: true),
            "algorithm" => TestTokens.Create(algorithm: Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha512),
            _ => "not-a-jwt"
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/pokemon");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(token, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("error_description", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Tokens_in_query_strings_or_cookies_do_not_authenticate()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("Cookie", "access_token=" + TestTokens.Create());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/pokemon?access_token=" + TestTokens.Create())).StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Health_probes_answer_anonymous_requests(string path)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Fact]
    public async Task Forged_oidc_callback_cannot_create_a_session()
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.GetAsync("/signin-oidc?code=forged&state=forged");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            cookie => cookie.StartsWith("pokemon.documentation="));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/pokemon")).StatusCode);
    }

    [Fact]
    public async Task Documentation_cookie_cannot_authorize_business_endpoints()
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = null;
        var options = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Pokemon.Api.Feature.Authentication.DocumentationAuthentication.Cookie);
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(2)
        };
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("sub", "documentation-user")], Pokemon.Api.Feature.Authentication.DocumentationAuthentication.Cookie));
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, properties, Pokemon.Api.Feature.Authentication.DocumentationAuthentication.Cookie);
        client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/scalar/v1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        foreach (var path in new[] { "/pokemon", "/moves", "/species", "/damage", "/battles/" + Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/battles", null)).StatusCode);
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    public async Task Protected_documentation_is_not_cached(string path)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.True(response.Headers.CacheControl!.NoStore);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Valid_token_opens_documentation_with_security_requirements()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/scalar/v1")).StatusCode);
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("bearer", doc["components"]!["securitySchemes"]!["Bearer"]!["scheme"]!.GetValue<string>());
        foreach (var path in doc["paths"]!.AsObject())
            foreach (var operation in path.Value!.AsObject())
                Assert.NotNull(operation.Value!["security"]![0]!["Bearer"]);
    }
}
