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

    /*
        The limiter partitions on the forwarded client address, so a caller who could choose
        that address could evade it by rotating a header. Each request here carries a
        different client-supplied entry and the same trailing entry, which is what the proxy
        appends and what ForwardLimit = 1 makes the middleware read. Raising that limit walks
        left into the forged values and this test stops seeing a 429.
    */
    [Fact]
    public async Task SpoofedForwardedForEntries_DoNotEscapeTheLimit()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 12; i++)
        {
            var response = await PostLogin(client, email, $"10.0.0.{i}, 203.0.113.7");
            statuses.Add(response.StatusCode);
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    /*
        The converse, and the only test here that fails if UseForwardedHeaders is removed
        outright: exhausting one client's window must not touch another's. Without the
        middleware TestServer leaves RemoteIpAddress null, every caller shares the "unknown"
        partition, and the second client inherits the first one's 429.
    */
    [Fact]
    public async Task DistinctForwardedClients_GetSeparatePartitions()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        for (var i = 0; i < 12; i++)
            await PostLogin(client, email, "198.51.100.1");

        var other = await PostLogin(client, email, "198.51.100.2");

        other.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    private static Task<HttpResponseMessage> PostLogin(HttpClient client, string email, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = "wrong password attempt" })
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);

        return client.SendAsync(request);
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
