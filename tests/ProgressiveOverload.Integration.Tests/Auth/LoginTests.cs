using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Infrastructure.Auth;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class LoginTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);
    private const string Password = "correct horse battery staple";

    public void Dispose() => _factory.Dispose();

    /*
        Wraps the real password hasher so the login timing defence can be proven without
        asserting wall-clock time, which would be flaky. Counting calls to VerifyDummy is
        deterministic: an early return that skips it (the exact regression mutation-tested
        during Task 8 review) makes the count wrong instead of just making a request faster.
    */
    private sealed class CountingPasswordHasher(IPasswordHasher inner) : IPasswordHasher
    {
        public int VerifyDummyCalls;
        public int VerifyCalls;

        public string Hash(string password) => inner.Hash(password);

        public bool Verify(string hash, string password)
        {
            Interlocked.Increment(ref VerifyCalls);
            return inner.Verify(hash: hash, password: password);
        }

        public bool VerifyDummy(string password)
        {
            Interlocked.Increment(ref VerifyDummyCalls);
            return inner.VerifyDummy(password);
        }
    }

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

        var unknownBody = JsonNode.Parse(await unknown.Content.ReadAsStringAsync())!.AsObject();
        var wrongBody = JsonNode.Parse(await wrong.Content.ReadAsStringAsync())!.AsObject();

        // Pin the fields a client (or an attacker) actually reads, individually, so a
        // failure here points straight at what changed rather than just "bodies differ".
        unknownBody["status"]!.ToString().ShouldBe(wrongBody["status"]!.ToString());
        unknownBody["title"]!.ToString().ShouldBe(wrongBody["title"]!.ToString());
        unknownBody["code"]!.ToString().ShouldBe(wrongBody["code"]!.ToString());

        // Only traceId is excluded, because it is a per-request correlation id stamped by
        // the framework's problem-details writer, uncorrelated with which email was tried
        // or which failure occurred - not an enumeration channel. Everything else must
        // still match: any other field that differs between the two responses still fails
        // this test.
        unknownBody.Remove("traceId");
        wrongBody.Remove("traceId");
        JsonNode.DeepEquals(unknownBody, wrongBody).ShouldBeTrue();
    }

    [Fact]
    public async Task Login_WithUnknownEmail_StillPerformsHashingWork()
    {
        // The real adapter does the actual PBKDF2 work; this decorator only counts calls
        // so the test can assert the timing defence ran without asserting wall-clock time,
        // which would be flaky.
        var counting = new CountingPasswordHasher(new PasswordHasherAdapter());

        using var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher>(counting);
            })).CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"{Guid.NewGuid():N}@example.com", password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Exactly one full-cost hash verification for the unmatched email, and zero real
        // verifications, since there was no hash to verify against. If a future change
        // short-circuits before VerifyDummy for an unknown email, this count goes to zero
        // and the test fails, even though the response above still looks correct.
        counting.VerifyDummyCalls.ShouldBe(1);
        counting.VerifyCalls.ShouldBe(0);
    }
}
