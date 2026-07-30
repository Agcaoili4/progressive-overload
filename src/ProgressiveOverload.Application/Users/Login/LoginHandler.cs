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

        // user?.PasswordHash is { } hash also covers a Google-only account: it has no
        // PasswordHash (null), so it falls to the VerifyDummy branch below and is
        // rejected. Someone who signed up with Google must not be able to authenticate
        // by supplying a password.
        //
        // When no user matched, or the matched user has no password hash, we still call
        // VerifyDummy instead of short-circuiting. VerifyDummy runs a real full-cost
        // PBKDF2 verification against a throwaway hash and always returns false, so a
        // failed login costs the same time whether or not the email is registered. An
        // early return here would reopen the timing side-channel this call exists to
        // close, even though the response content stays identical.
        var passwordValid = user?.PasswordHash is { } hash
            ? passwordHasher.Verify(hash: hash, password: command.Password)
            : passwordHasher.VerifyDummy(command.Password);

        if (!passwordValid)
            return Result<AuthResult>.Failure(AuthErrors.InvalidCredentials);

        var (raw, tokenHash) = tokens.CreateRefreshToken();

        // user is guaranteed non-null here: if it were null, passwordValid could only
        // have come from VerifyDummy, which always returns false, and we would already
        // have returned above. The compiler can't see that correlation, hence the null-
        // forgiving operator.
        db.RefreshTokens.Add(RefreshToken.Issue(
            user!.Id, tokenHash, clock.UtcNow, TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)));

        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
