using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.Register;

public sealed class RegisterHandler(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        // This pre-check only buys a clean error message; it does not prevent duplicates
        // under concurrency by itself. The unique index on Users.Email is what actually
        // does that, which is why the endpoint also catches DbUpdateException.
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Result<AuthResult>.Failure(UserErrors.EmailAlreadyRegistered);

        var hash = passwordHasher.Hash(command.Password);
        var creation = User.CreateWithPassword(email, hash, command.DisplayName);
        if (creation.IsFailure)
            return Result<AuthResult>.Failure(creation.Error);

        var user = creation.Value;
        var (raw, tokenHash) = tokens.CreateRefreshToken();

        var refreshToken = RefreshToken.Issue(
            user.Id, tokenHash, clock.UtcNow, TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays));

        db.Users.Add(user);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
