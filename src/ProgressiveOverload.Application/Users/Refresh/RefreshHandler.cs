using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;

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

        // Detach `stored` so the change tracker won't queue its own UPDATE for this row.
        // stored.Redeem() above only sets RedeemedAt in memory, never in the database, and
        // is kept solely for its ordered validation (reused/revoked/expired) and error
        // codes; the durable write is the conditional ExecuteUpdateAsync claim below, or
        // no write at all. Without detaching, the success path's later SaveChangesAsync
        // would issue its own UPDATE for `stored`, racing and duplicating that claim.
        db.Entry(stored).State = EntityState.Detached;

        if (redemption.IsFailure)
        {
            // A replayed token means theft. Thief and victim are indistinguishable here,
            // so every session descended from this sign-in ends and both must sign in again.
            if (redemption.Error == AuthErrors.RefreshTokenReused)
                await RevokeFamily(stored.FamilyId, ct);

            return Result<AuthResult>.Failure(redemption.Error);
        }

        // Durable, atomic claim of this token: a compare-and-swap that only one racing
        // request can win, because Postgres serializes concurrent UPDATEs against the
        // same row. The in-memory check above only proves RedeemedAt was null in the
        // snapshot loaded earlier - it says nothing about what happened between that read
        // and now. Two requests racing with the SAME token (a thief replaying alongside
        // the legitimate client) can both pass that check.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RedeemedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RedeemedAt, clock.UtcNow), ct);

        if (claimed == 0)
        {
            // Lost the race: something else redeemed this exact token between the read
            // and the write above. Legitimate client or racing thief cannot be told apart,
            // so this is handled identically to replaying an already-redeemed token: kill
            // the family.
            await RevokeFamily(stored.FamilyId, ct);
            return Result<AuthResult>.Failure(AuthErrors.RefreshTokenReused);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null)
            return Result<AuthResult>.Failure(AuthErrors.RefreshTokenInvalid);

        var (raw, newHash) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Issue(
            user.Id, newHash, clock.UtcNow,
            TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays), stored.FamilyId));

        await db.SaveChangesAsync(ct);

        // Post-insert race check: a racing loser's family revoke runs
        // `WHERE family_id = F AND revoked_at IS NULL`, which can only touch rows that
        // already exist. If that revoke commits before the SaveChangesAsync above lands,
        // the just-inserted row did not exist yet and cannot have been reached - a locked
        // row still needs a row to lock. So check back after inserting: if the family was
        // revoked during this write, this session is compromised too. Revoke again (the
        // sweep now catches the newly inserted token) and fail.
        var familyRevoked = await db.RefreshTokens
            .AnyAsync(t => t.FamilyId == stored.FamilyId && t.RevokedAt != null, ct);
        if (familyRevoked)
        {
            await RevokeFamily(stored.FamilyId, ct);
            return Result<AuthResult>.Failure(AuthErrors.RefreshTokenReused);
        }

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }

    /*
        ExecuteUpdateAsync runs immediately against the database and bypasses the change
        tracker. Every caller returns a failure right afterward with no further
        SaveChangesAsync in the same Handle call, so nothing in this request can later
        disagree with what was just written.
    */
    private async Task RevokeFamily(Guid familyId, CancellationToken ct) =>
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);
}
