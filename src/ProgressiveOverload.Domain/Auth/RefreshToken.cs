using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Domain.Auth;

public sealed class RefreshToken
{
    private RefreshToken() { } // EF Core

    private RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the opaque token. The raw token is never persisted.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>
    /// Shared by every token descended from one sign-in. Reuse of any token in the family
    /// revokes the entire family.
    /// </summary>
    public Guid FamilyId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RedeemedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RedeemedAt is null && RevokedAt is null;

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime,
        Guid? familyId = null) =>
        new(userId, tokenHash, familyId ?? Guid.CreateVersion7(), now + lifetime);

    public Result Redeem(DateTimeOffset now)
    {
        // Order matters. A token presented twice means it was probably stolen, and the
        // caller reacts by killing the whole family — so reuse must be reported even if
        // the token has since been revoked or expired.
        if (RedeemedAt is not null) return Result.Failure(AuthErrors.RefreshTokenReused);
        if (RevokedAt is not null) return Result.Failure(AuthErrors.RefreshTokenInvalid);
        if (now > ExpiresAt) return Result.Failure(AuthErrors.RefreshTokenExpired);

        RedeemedAt = now;
        return Result.Success();
    }

    public void Revoke()
    {
        // ??= keeps the first revocation time. Revoking a family re-revokes tokens that
        // are already dead, and we want when it actually happened.
        RevokedAt ??= DateTimeOffset.UtcNow;
    }
}
