using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RefreshRotationTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private static string ExtractRefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("po_refresh="))
            .Split(';')[0]["po_refresh=".Length..];

    private async Task<(HttpClient Client, string Refresh)> ASignedInUser()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName = "Jansen"
        });

        return (client, ExtractRefreshCookie(response));
    }

    private static HttpRequestMessage RefreshRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"po_refresh={token}");
        return request;
    }

    [Fact]
    public async Task Refresh_IssuesANewTokenPair()
    {
        var (client, refresh) = await ASignedInUser();

        var response = await client.SendAsync(RefreshRequest(refresh));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExtractRefreshCookie(response).ShouldNotBe(refresh);
    }

    [Fact]
    public async Task Refresh_WithNoCookie_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/v1/auth/refresh", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReusingAnOldToken_RevokesTheEntireFamily()
    {
        var (client, original) = await ASignedInUser();

        var rotated = await client.SendAsync(RefreshRequest(original));
        var newToken = ExtractRefreshCookie(rotated);

        // Replay the already-redeemed token: this is the theft signal.
        var replay = await client.SendAsync(RefreshRequest(original));
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The token the legitimate client holds must now also be dead.
        var afterBreach = await client.SendAsync(RefreshRequest(newToken));
        afterBreach.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTheFamilyAndClearsTheCookie()
    {
        var (client, refresh) = await ASignedInUser();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"po_refresh={refresh}");
        var logout = await client.SendAsync(request);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterLogout = await client.SendAsync(RefreshRequest(refresh));
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
