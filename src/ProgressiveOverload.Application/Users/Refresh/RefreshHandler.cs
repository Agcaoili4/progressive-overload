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

        // stored.Redeem() above only mutates RedeemedAt on the tracked in-memory entity -
        // it does not touch the database. We keep it purely for its ordered validation
        // (reused/revoked/expired) and error codes. The actual durable write happens
        // either through the conditional ExecuteUpdateAsync claim below or not at all in
        // this method, so detach `stored` now to stop the change tracker from also queuing
        // an UPDATE for this row. Without this, the later SaveChangesAsync in the success
        // path would issue its own UPDATE for `stored` racing/duplicating the raw claim
        // below, which is exactly the kind of two-writers-one-row situation this fix
        // exists to eliminate.
        db.Entry(stored).State = EntityState.Detached;

        if (redemption.IsFailure)
        {
            // Replaying a redeemed token means the token was captured — we cannot tell
            // whether we are talking to the thief or the victim, so end every session
            // descended from this sign-in and force a fresh login.
            if (redemption.Error == AuthErrors.RefreshTokenReused)
                await RevokeFamily(stored.FamilyId, ct);

            return Result<AuthResult>.Failure(redemption.Error);
        }

        // Durable, atomic claim of this token. The in-memory check above only proves
        // RedeemedAt was null in the snapshot we loaded - it says nothing about what
        // happened between that read and now. Two requests racing with the SAME token
        // (a thief replaying alongside the legitimate client) can both pass the check
        // above; this conditional UPDATE is a compare-and-swap that only one of them can
        // win, because Postgres serializes concurrent UPDATEs against the same row.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RedeemedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RedeemedAt, clock.UtcNow), ct);

        if (claimed == 0)
        {
            // Lost the race: something else redeemed this exact token between our read
            // and our write. We cannot tell whether that was the legitimate client or a
            // thief who raced it, so this is indistinguishable from - and handled
            // identically to - replaying an already-redeemed token: kill the family.
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

        // A racing loser that lost the claim above revokes the family with
        // `WHERE family_id = F AND revoked_at IS NULL`, but that UPDATE can only ever
        // touch rows that already exist. If the loser's revoke commits before this
        // request's SaveChangesAsync above lands, the row we just inserted did not exist
        // yet and the revocation cannot have reached it - a locked row still needs a row
        // to lock, and this one has none until now. So after inserting, check back: if
        // the family was revoked while we were writing, this session is compromised too.
        // Revoke again (this sweep now catches the token we just inserted) and fail.
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

    private async Task RevokeFamily(Guid familyId, CancellationToken ct) =>
        // ExecuteUpdateAsync runs immediately against the database and bypasses the
        // change tracker. Every caller of this method returns a failure result right
        // afterward without any further SaveChangesAsync in the same Handle call, so
        // there is nothing left in this request that could disagree with what was just
        // written.
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);
}
