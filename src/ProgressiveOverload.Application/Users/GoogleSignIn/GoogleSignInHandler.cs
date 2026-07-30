using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.GoogleSignIn;

public sealed class GoogleSignInHandler(
    AppDbContext db,
    IGoogleTokenValidator google,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(GoogleSignInCommand command, CancellationToken ct)
    {
        var validation = await google.Validate(command.IdToken, ct);
        if (validation.IsFailure)
            return Result<AuthResult>.Failure(validation.Error);

        var payload = validation.Value;

        /*
            Linking an identity to an existing account on the strength of an unverified
            email address is a straightforward takeover: anyone who can obtain a Google
            token for an unverified address matching a registered user's email would
            otherwise walk into that account. Google sets this claim; trust it only when
            it is true, and stop before creating or linking anything.
        */
        if (!payload.EmailVerified)
            return Result<AuthResult>.Failure(AuthErrors.GoogleEmailUnverified);

        var email = payload.Email.Trim().ToLowerInvariant();

        // Subject first, email second: Google subjects never change but a user's email
        // can, so matching on email alone would eventually attach one person's sign-in
        // to another person's account.
        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == payload.Subject, ct)
                   ?? await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            var fallbackName = email.Split('@')[0];
            // Truncated: the local part of an email can exceed MaxDisplayNameLength, and this
            // only runs when Google gave no name, so a long address must not fail sign-in.
            if (fallbackName.Length > User.MaxDisplayNameLength)
                fallbackName = fallbackName[..User.MaxDisplayNameLength];

            var creation = User.CreateFromGoogle(email, payload.Subject, payload.Name ?? fallbackName);
            if (creation.IsFailure) return Result<AuthResult>.Failure(creation.Error);

            user = creation.Value;
            db.Users.Add(user);
        }
        else
        {
            var link = user.LinkGoogleAccount(payload.Subject);
            if (link.IsFailure) return Result<AuthResult>.Failure(link.Error);
        }

        var (raw, tokenHash) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Issue(
            user.Id, tokenHash, clock.UtcNow, TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)));

        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
