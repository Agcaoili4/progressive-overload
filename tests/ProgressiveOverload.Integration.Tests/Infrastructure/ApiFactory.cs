using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProgressiveOverload.Integration.Tests.Infrastructure;

public sealed class ApiFactory(PostgresFixture fixture) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fixture.ConnectionString,
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes",
                ["Jwt:Issuer"] = "progressiveoverload",
                ["Jwt:Audience"] = "progressiveoverload"
            }));

        return base.CreateHost(builder);
    }
}
