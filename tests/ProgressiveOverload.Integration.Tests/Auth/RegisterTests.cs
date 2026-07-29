using System.Net;
using System.Net.Http.Json;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RegisterTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private static object ARegistration(string? email = null) => new
    {
        email = email ?? $"{Guid.NewGuid():N}@example.com",
        password = "correct horse battery staple",
        displayName = "Jansen"
    };

    [Fact]
    public async Task Register_ReturnsAccessTokenAndSetsRefreshCookie()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.DisplayName.ShouldBe("Jansen");

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        cookies.ShouldContain(c => c.StartsWith("po_refresh=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_NeverReturnsTheRefreshTokenInTheBody()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration());

        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain("refresh", Case.Insensitive);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration(email));
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration(email));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_RejectsShortPassword()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "short",
            displayName = "Jansen"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
