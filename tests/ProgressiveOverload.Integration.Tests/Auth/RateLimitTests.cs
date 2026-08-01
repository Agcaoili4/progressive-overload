using System.Net;
using System.Net.Http.Json;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RateLimitTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RepeatedFailedLogins_AreRateLimited()
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        var statuses = await Hammer("/api/v1/auth/login",
            new { email, password = "wrong password attempt" });

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    /*
        Register and Google sign-in sit behind the same policy and were both flagged as
        unthrottled in earlier reviews, so neither is left to inference. Each payload is
        deliberately invalid: the limiter runs before the endpoint, so a rejected request
        still consumes a permit, and nothing here depends on creating real accounts.
    */
    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/google")]
    public async Task RepeatedUnauthenticatedRequests_AreRateLimited(string path)
    {
        var payload = path.EndsWith("register")
            ? new { email = "not-an-email", password = "x", displayName = "", idToken = "" }
            : new { email = "", password = "", displayName = "", idToken = "" };

        var statuses = await Hammer(path, payload);

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    // Twelve against a PermitLimit of 10, so the window is exceeded with room to spare.
    private async Task<List<HttpStatusCode>> Hammer(string path, object payload)
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 12; i++)
        {
            var response = await client.PostAsJsonAsync(path, payload);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }
}
