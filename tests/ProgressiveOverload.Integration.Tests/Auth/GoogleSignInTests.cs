using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class GoogleSignInTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private sealed class FakeGoogleValidator : IGoogleTokenValidator
    {
        public GooglePayload? Payload { get; set; }

        public Task<Result<GooglePayload>> Validate(string idToken, CancellationToken ct) =>
            Task.FromResult(Payload is null
                ? Result<GooglePayload>.Failure(AuthErrors.GoogleTokenInvalid)
                : Result<GooglePayload>.Success(Payload));
    }

    private HttpClient ClientWith(GooglePayload? payload)
    {
        var fake = new FakeGoogleValidator { Payload = payload };
        return _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IGoogleTokenValidator>(fake))).CreateClient();
    }

    [Fact]
    public async Task GoogleSignIn_CreatesAccountForNewEmail()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = ClientWith(new GooglePayload("sub-1", email, EmailVerified: true, "Jansen"));

        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GoogleSignIn_RejectsUnverifiedEmail()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = ClientWith(new GooglePayload("sub-2", email, EmailVerified: false, "Jansen"));

        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GoogleSignIn_LinksToExistingPasswordAccountWithSameEmail()
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        var passwordClient = _factory.CreateClient();
        var registered = await passwordClient.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "correct horse battery staple", displayName = "Jansen" });
        var originalUserId = (await registered.Content
            .ReadFromJsonAsync<ProgressiveOverload.Application.Users.AuthResponse>())!.UserId;

        var client = ClientWith(new GooglePayload("sub-3", email, EmailVerified: true, "Jansen"));
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<ProgressiveOverload.Application.Users.AuthResponse>();
        body!.UserId.ShouldBe(originalUserId);
    }

    [Fact]
    public async Task GoogleSignIn_RejectsInvalidToken()
    {
        var client = ClientWith(null);
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "garbage" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
