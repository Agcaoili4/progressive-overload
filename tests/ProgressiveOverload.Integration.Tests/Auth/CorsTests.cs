using System.Net;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

/*
    A browser enforces CORS, so none of this is a server-side access control — the checks
    here are about whether the web client can read responses at all. Getting it wrong does
    not leak data; it makes the API unusable from the browser in ways that surface as
    "network error" rather than anything diagnosable.
*/
[Collection(nameof(PostgresCollection))]
public sealed class CorsTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return request;
    }

    [Fact]
    public async Task Preflight_FromTheWebClientOrigin_IsAllowedWithCredentials()
    {
        var response = await _factory.CreateClient()
            .SendAsync(Preflight("/api/v1/auth/login", ApiFactory.AllowedOrigin));

        response.Headers.GetValues("Access-Control-Allow-Origin")
            .ShouldContain(ApiFactory.AllowedOrigin);

        // Without this the browser drops the refresh cookie on /auth/refresh and the
        // session silently fails to survive a page reload.
        response.Headers.GetValues("Access-Control-Allow-Credentials").ShouldContain("true");
    }

    [Fact]
    public async Task Preflight_FromAnUnknownOrigin_IsNotAllowed()
    {
        var response = await _factory.CreateClient()
            .SendAsync(Preflight("/api/v1/auth/login", "https://attacker.example"));

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    /*
        A 401 raised by UseAuthorization, not by an endpoint. That distinction is the whole
        point: endpoint responses pass back out through UseCors wherever it sits, so testing
        this against a failed login proves nothing. Only a challenge raised upstream of the
        endpoint can lose the header, and it does so only if UseCors sits after
        UseAuthorization — after UseAuthentication alone is still fine.
    */
    [Fact]
    public async Task AnUnauthenticatedRequest_StillCarriesTheCorsHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Add("Origin", ApiFactory.AllowedOrigin);

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .ShouldContain(ApiFactory.AllowedOrigin);
    }
}
