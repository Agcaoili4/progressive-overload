using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProgressiveOverload.Integration.Tests.Infrastructure;

public sealed class ApiFactory(PostgresFixture fixture) : WebApplicationFactory<Program>
{
    /*
        Exposed so tests that mint their own tokens sign with exactly what the running app
        validates against. A private copy in the test would drift the moment either side
        changed, and a token-validation test that signs with the wrong key passes for the
        wrong reason.
    */
    public const string SigningKey = "integration-test-signing-key-at-least-32-bytes";
    public const string Issuer = "progressiveoverload";
    public const string Audience = "progressiveoverload";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fixture.ConnectionString,
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience
            }));

        return base.CreateHost(builder);
    }
}
