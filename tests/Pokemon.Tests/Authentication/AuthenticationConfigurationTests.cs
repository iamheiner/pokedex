using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pokemon.Api.Feature.Authentication;

namespace Pokemon.Tests.Authentication;

public sealed class AuthenticationConfigurationTests
{
    /// <summary>
    /// Comprueba que una configuración de autenticación ausente o insegura impide iniciar la API.
    /// </summary>
    [Theory]
    [InlineData(null, "pokemon-api")]
    [InlineData("invalid", "pokemon-api")]
    [InlineData("http://identity.test/realms/pokemon", "pokemon-api")]
    [InlineData("https://identity.test/realms/pokemon", "")]
    public void Missing_or_insecure_configuration_fails_closed(string? authority, string audience)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = authority,
            ["Authentication:Audience"] = audience
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddApiAuthentication(configuration));
    }
}
