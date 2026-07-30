using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.Refresh;

public sealed class RefreshHandler(
    AppDbContext db,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(string rawToken, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            return Result<AuthResult>.Failure(AuthErrors.RefreshTokenInvalid);

        var redemption = stored.Redeem(clock.UtcNow);
        if (redemption.IsFailure)
        {
            // Replaying a redeemed token means the token was captured — we cannot tell
            // whether we are talking to the thief or the victim, so end every session
            // descended from this sign-in and force a fresh login.
            if (redemption.Error == AuthErrors.RefreshTokenReused)
            {
                // ExecuteUpdateAsync runs immediately against the database and bypasses the
                // change tracker, so `stored` (and any other tracked entity in this family)
                // would be stale after this call. That is safe here only because we return
                // right below without touching the change tracker again - there is no later
                // SaveChangesAsync in this branch that could stomp on or disagree with what
                // was just written.
                await db.RefreshTokens
                    .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);
            }

            return Result<AuthResult>.Failure(redemption.Error);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null)
            return Result<AuthResult>.Failure(AuthErrors.RefreshTokenInvalid);

        var (raw, newHash) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Issue(
            user.Id, newHash, clock.UtcNow,
            TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays), stored.FamilyId));

        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
