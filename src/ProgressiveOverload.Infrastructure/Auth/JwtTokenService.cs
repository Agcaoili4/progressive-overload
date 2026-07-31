using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = clock.UtcNow.UtcDateTime,
            Expires = clock.UtcNow.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                /*
                    Carried for a future password-change or global sign-out; nothing compares
                    it today, so SetPasswordHash rotating SecurityStamp invalidates nothing.
                    An access token stays valid for its full lifetime, which means revoking a
                    session is a matter of revoking the refresh-token family — this token
                    cannot be recalled before it expires.
                */
                ["stamp"] = user.SecurityStamp.ToString()
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public (string Raw, string Hash) CreateRefreshToken()
    {
        // The refresh token is opaque random bytes, not a JWT — there is nothing to encode
        // in it and a JWT would only invite someone to trust its contents.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (raw, HashRefreshToken(raw));
    }

    /*
        Stored SHA-256 hashed so a database leak yields no usable sessions. Plain SHA-256
        (rather than a slow KDF like PBKDF2/bcrypt) is correct here because the token is
        256 bits of CSPRNG output, not a human-chosen secret, so there is no dictionary to
        attack and a slow hash would buy nothing but wasted CPU.
    */
    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
