using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Application.Users.Login;

public sealed class LoginHandler(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(LoginCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        // VerifyDummy runs even when no user matched or the matched user has no password
        // hash, instead of short-circuiting: it performs a real full-cost PBKDF2
        // verification against a throwaway hash and always returns false, so a failed
        // login costs the same time whether or not the email is registered. An early
        // return here would reopen the timing side-channel this call exists to close,
        // even though the response content stays identical. This also covers Google-only
        // accounts (no PasswordHash, so `hash` is null): they fall to the VerifyDummy
        // branch and are rejected, since signing up with Google must not allow
        // authenticating with a password.
        var passwordValid = user?.PasswordHash is { } hash
            ? passwordHasher.Verify(hash: hash, password: command.Password)
            : passwordHasher.VerifyDummy(command.Password);

        if (!passwordValid)
            return Result<AuthResult>.Failure(AuthErrors.InvalidCredentials);

        var (raw, tokenHash) = tokens.CreateRefreshToken();

        // user is guaranteed non-null here: if it were null, passwordValid could only
        // have come from VerifyDummy, which always returns false, so execution would
        // already have returned above. The compiler cannot see that correlation, hence
        // the null-forgiving operator.
        db.RefreshTokens.Add(RefreshToken.Issue(
            user!.Id, tokenHash, clock.UtcNow, TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)));

        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
