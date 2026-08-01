using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

/*
    Guards the JWT bearer parameters in Program.cs. Every token here is signed with the key
    the app validates against and names a user that really exists, so the only thing wrong
    with it is the flaw under test — a token rejected for being unrecognised garbage would
    prove nothing about issuer, audience or lifetime validation.
*/
[Collection(nameof(PostgresCollection))]
public sealed class JwtValidationTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    public enum TokenFlaw
    {
        WrongIssuer,
        WrongAudience,
        Expired,
        TamperedSignature
    }

    [Theory]
    [InlineData(TokenFlaw.WrongIssuer)]
    [InlineData(TokenFlaw.WrongAudience)]
    [InlineData(TokenFlaw.Expired)]
    [InlineData(TokenFlaw.TamperedSignature)]
    public async Task GetMe_RejectsAnOtherwiseValidTokenCarrying(TokenFlaw flaw)
    {
        var userId = await ARegisteredUserId();
        var now = DateTime.UtcNow;

        var token = flaw switch
        {
            TokenFlaw.WrongIssuer =>
                Mint(userId, "https://attacker.example", ApiFactory.Audience, now, now.AddMinutes(15)),
            TokenFlaw.WrongAudience =>
                Mint(userId, ApiFactory.Issuer, "some-other-api", now, now.AddMinutes(15)),
            // Two hours stale, so it fails on expiry rather than on the 30-second ClockSkew
            // boundary. NotBefore moves with it, or the token would be malformed instead.
            TokenFlaw.Expired =>
                Mint(userId, ApiFactory.Issuer, ApiFactory.Audience, now.AddHours(-2), now.AddHours(-1)),
            TokenFlaw.TamperedSignature =>
                TamperSignature(Mint(userId, ApiFactory.Issuer, ApiFactory.Audience, now, now.AddMinutes(15))),
            _ => throw new ArgumentOutOfRangeException(nameof(flaw))
        };

        var response = await Get(token, "/api/v1/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /*
        The control for the four cases above. Without it they could all pass because the
        minting helper produces a token the app rejects for some unrelated reason, which
        would make the whole class a false negative.
    */
    [Fact]
    public async Task GetMe_AcceptsACorrectlyMintedToken()
    {
        var userId = await ARegisteredUserId();
        var now = DateTime.UtcNow;

        var token = Mint(userId, ApiFactory.Issuer, ApiFactory.Audience, now, now.AddMinutes(15));

        var response = await Get(token, "/api/v1/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<Guid> ARegisteredUserId()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName = "Jansen"
        });

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.UserId;
    }

    private async Task<HttpResponseMessage> Get(string token, string path)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(path);
    }

    // Mirrors JwtTokenService.CreateAccessToken. Only `sub` is read by CurrentUser, but the
    // stamp claim is carried so the token matches the real shape.
    private static string Mint(Guid userId, string issuer, string audience, DateTime issuedAt, DateTime expires)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.SigningKey)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                ["stamp"] = Guid.NewGuid().ToString()
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // Flips one character of the signature segment only, leaving the header and payload
    // intact, so the token still parses and fails on the HMAC rather than on its shape.
    private static string TamperSignature(string token)
    {
        var parts = token.Split('.');
        parts[2] = (parts[2][0] == 'A' ? 'B' : 'A') + parts[2][1..];
        return string.Join('.', parts);
    }
}
