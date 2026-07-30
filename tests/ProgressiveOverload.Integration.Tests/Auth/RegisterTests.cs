using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
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

    /*
        Pulls the raw refresh token out of a Set-Cookie header the way a real cookie jar
        would: take the name=value pair, then percent-decode it, since ASP.NET encodes the
        value on write (the raw token is standard Base64 and can contain '+', '/', '=').
    */
    private static string ExtractRefreshCookieValue(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("po_refresh="));
        var nameValue = header.Split(';')[0];
        var encoded = nameValue["po_refresh=".Length..];
        return Uri.UnescapeDataString(encoded);
    }

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

        var cookieHeader = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("po_refresh="));

        // Every attribute that makes the cookie safe must be asserted, not just HttpOnly -
        // dropping Secure or SameSite=Strict (or widening Path) would leave this cookie
        // exploitable while every other check here still passed.
        cookieHeader.ShouldContain("httponly", Case.Insensitive);
        cookieHeader.ShouldContain("secure", Case.Insensitive);
        cookieHeader.ShouldContain("samesite=strict", Case.Insensitive);
        cookieHeader.ShouldContain("path=/api/v1/auth", Case.Insensitive);

        // Prove the token itself round-trips: percent-decode what a client would store,
        // hash it the same way the server does, and check it matches what got persisted.
        // This is the property every future session depends on.
        var rawToken = ExtractRefreshCookieValue(response);

        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await db.RefreshTokens.SingleAsync(t => t.UserId == body.UserId);
        tokens.HashRefreshToken(rawToken).ShouldBe(stored.TokenHash);
    }

    [Fact]
    public async Task Register_NeverReturnsTheRefreshTokenInTheBody()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration());

        // A leak under a 404 (endpoint missing) or under some other property name would
        // slip past a bare string search, so pin the real success path first.
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var rawToken = ExtractRefreshCookieValue(response);
        var percentEncoded = Uri.EscapeDataString(rawToken);

        var responseBody = await response.Content.ReadAsStringAsync();

        // Check both forms: the raw value and the percent-encoded form the cookie itself
        // uses, so an accidental copy of the exact cookie bytes into the body is caught too.
        responseBody.ShouldNotContain(rawToken);
        responseBody.ShouldNotContain(percentEncoded);
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
