using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Application.Users.Logout;

public sealed class LogoutHandler(AppDbContext db, ITokenService tokens, IClock clock)
{
    public async Task<Result> Handle(string? rawToken, CancellationToken ct)
    {
        // Logout always reports success, even for a missing, unknown, or already-revoked
        // token. Telling an unauthenticated caller whether a token was valid gives away
        // information for no benefit - it hands an attacker a way to probe which tokens
        // are live.
        if (string.IsNullOrWhiteSpace(rawToken)) return Result.Success();

        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return Result.Success();

        await db.RefreshTokens
            .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        return Result.Success();
    }
}
