using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TAMS.Infrastructure;
using TAMS.Infrastructure.Security;

namespace TAMS.Integration.Tests;

/// <summary>
/// P6 config-hardening regression: the JWT options validator wired by
/// AddInfrastructure (ValidateOnStart) must reject a missing or too-short signing
/// key, so a deployment can never boot on a weak/empty key. Tested against a
/// minimal DI container built the same way the API/Worker composition roots build it.
/// </summary>
public sealed class ConfigValidationTests
{
    private static IServiceProvider BuildProvider(string signingKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A valid connection string so AddInfrastructure's own null-check passes;
                // it is never opened in this test.
                ["ConnectionStrings:Default"] = "Server=(localdb)\\MSSQLLocalDB;Database=TAMS_CfgTest;Trusted_Connection=True;TrustServerCertificate=true",
                ["Jwt:Issuer"] = "TAMS",
                ["Jwt:Audience"] = "TAMS.Client",
                ["Jwt:SigningKey"] = signingKey,
            })
            .Build();

        return new ServiceCollection()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }

    [Theory]
    [InlineData("")]              // empty
    [InlineData("too-short-key")] // < 32 bytes
    public void InvalidSigningKey_FailsValidation(string badKey)
    {
        using var provider = (ServiceProvider)BuildProvider(badKey);

        var act = () => provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain("SigningKey");
    }

    [Fact]
    public void ValidSigningKey_PassesValidation()
    {
        using var provider = (ServiceProvider)BuildProvider(
            "a-perfectly-fine-signing-key-that-is-well-over-32-bytes-long");

        var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        options.SigningKey.Should().NotBeNullOrWhiteSpace();
        options.Issuer.Should().Be("TAMS");
    }
}
