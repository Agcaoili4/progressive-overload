using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Abstractions;

public interface ITokenService
{
    string CreateAccessToken(User user);

    /* Returns the raw token to hand to the client, and the hash to persist. */
    (string Raw, string Hash) CreateRefreshToken();

    string HashRefreshToken(string raw);
}
