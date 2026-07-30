using System.Net;
using System.Net.Http.Json;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RefreshRotationTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private static string RawRefreshCookieHeader(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("po_refresh="));

    private static string ExtractRefreshCookie(HttpResponseMessage response) =>
        RawRefreshCookieHeader(response).Split(';')[0]["po_refresh=".Length..];

    // Every Set-Cookie attribute except the cookie value itself and `expires` (which
    // legitimately differs: login sets a future expiry, logout sets an already-past one).
    // A browser only treats logout's Set-Cookie as overwriting login's cookie if Path,
    // Secure, SameSite, and HttpOnly all match exactly - so this is what actually needs
    // to be equal between the two, not the header string as a whole.
    private static IEnumerable<string> CookieAttributes(HttpResponseMessage response) =>
        RawRefreshCookieHeader(response)
            .Split(';')
            .Skip(1)
            .Select(a => a.Trim().ToLowerInvariant())
            .Where(a => !a.StartsWith("expires=", StringComparison.Ordinal))
            .OrderBy(a => a, StringComparer.Ordinal);

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

    [Fact]
    public async Task ConcurrentReplayOfTheSameToken_LeavesNoUsableSession()
    {
        var (client, original) = await ASignedInUser();

        /* Fire both requests for the SAME token without awaiting in between - this is the
           race a thief creates by replaying a stolen token at (roughly) the same moment
           the legitimate client refreshes with it. Two interleavings are both legitimate
           outcomes of RefreshHandler's conditional claim + post-insert re-check:
             - one request wins the claim and returns 200, the other loses and returns 401
             - the loser's RevokeFamily commits before the winner's insert lands, the
               winner's post-insert re-check then finds the family already revoked and
               also returns 401, so BOTH requests come back 401
           The second case is not a bug - it is the more conservative resolution of a
           detected race, since a race is by definition indistinguishable from a replay.
           Do not tighten the assertions below to require exactly one 200; that assumption
           does not hold under the second interleaving. */
        var firstCall = client.SendAsync(RefreshRequest(original));
        var secondCall = client.SendAsync(RefreshRequest(original));
        var responses = await Task.WhenAll(firstCall, secondCall);

        var statusCodes = responses.Select(r => r.StatusCode).ToArray();
        statusCodes.Count(s => s == HttpStatusCode.Unauthorized).ShouldBeGreaterThanOrEqualTo(1);
        statusCodes.Count(s => s == HttpStatusCode.OK).ShouldBeLessThanOrEqualTo(1);

        // The property that actually matters: whatever happened above, no token
        // descended from this session works afterward - the original token is dead, and
        // so is any token a 200 response issued.
        var tokensToCheck = responses
            .Where(r => r.StatusCode == HttpStatusCode.OK)
            .Select(ExtractRefreshCookie)
            .Append(original);

        foreach (var token in tokensToCheck)
        {
            var afterRace = await client.SendAsync(RefreshRequest(token));
            afterRace.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Logout_ClearsTheCookieWithTheSameAttributesLoginSetItWith()
    {
        var client = _factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName = "Jansen"
        });
        var refresh = ExtractRefreshCookie(registerResponse);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"po_refresh={refresh}");
        var logoutResponse = await client.SendAsync(request);

        CookieAttributes(logoutResponse).ShouldBe(CookieAttributes(registerResponse));
    }
}
