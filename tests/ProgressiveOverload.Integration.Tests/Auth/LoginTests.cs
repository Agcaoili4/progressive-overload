using System.Net;
using System.Net.Http.Json;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class LoginTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);
    private const string Password = "correct horse battery staple";

    public void Dispose() => _factory.Dispose();

    private async Task<(HttpClient Client, string Email)> ARegisteredUser()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = Password, displayName = "Jansen" });

        return (client, email);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var (client, email) = await ARegisteredUser();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        var (client, email) = await ARegisteredUser();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = email.ToUpperInvariant(), password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var (client, email) = await ARegisteredUser();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "not the right password at all" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_AreIndistinguishable()
    {
        var (client, email) = await ARegisteredUser();

        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"{Guid.NewGuid():N}@example.com", password = Password });
        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "definitely wrong password" });

        unknown.StatusCode.ShouldBe(wrong.StatusCode);
        (await unknown.Content.ReadAsStringAsync())
            .ShouldBe(await wrong.Content.ReadAsStringAsync());
    }
}
