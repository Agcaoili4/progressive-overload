using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Infrastructure.Auth;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

public sealed class TokenServiceTests
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private static JwtTokenService NewService() =>
        new(Options.Create(new JwtOptions
        {
            SigningKey = "a-test-signing-key-that-is-at-least-32-bytes-long",
            AccessTokenMinutes = 15
        }), new FixedClock());

    [Fact]
    public void AccessToken_CarriesSubjectAndSecurityStamp()
    {
        var user = User.CreateWithPassword("a@b.com", "hash", "Jansen").Value;
        var token = new JsonWebTokenHandler().ReadJsonWebToken(NewService().CreateAccessToken(user));

        token.GetClaim(JwtRegisteredClaimNames.Sub).Value.ShouldBe(user.Id.ToString());
        token.GetClaim("stamp").Value.ShouldBe(user.SecurityStamp.ToString());
    }

    [Fact]
    public void AccessToken_ExpiresWithinTheConfiguredWindow()
    {
        var user = User.CreateWithPassword("a@b.com", "hash", "Jansen").Value;
        var token = new JsonWebTokenHandler().ReadJsonWebToken(NewService().CreateAccessToken(user));

        token.ValidTo.ShouldBe(new DateTime(2026, 7, 27, 12, 15, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RefreshTokens_AreUniqueAndHashDeterministically()
    {
        var service = NewService();
        var (rawA, hashA) = service.CreateRefreshToken();
        var (rawB, _) = service.CreateRefreshToken();

        rawA.ShouldNotBe(rawB);
        service.HashRefreshToken(rawA).ShouldBe(hashA);
        hashA.Length.ShouldBe(64);
    }
}
