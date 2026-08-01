using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";
    public string ClientId { get; init; } = string.Empty;
}

public sealed class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    public async Task<Result<GooglePayload>> Validate(string idToken, CancellationToken ct)
    {
        /*
            An empty ClientId means the audience list passed to the Google library is
            meaningless, and it will not meaningfully restrict which application a token was
            issued for - accepting tokens minted for any Google OAuth client, not just this
            one. Configuration will not supply a client id in development or in tests, so
            failing here (rather than trusting an unpinned audience) is what keeps that
            state safe: an unconfigured client id disables Google sign-in instead of
            weakening the audience check.
        */
        if (string.IsNullOrWhiteSpace(options.Value.ClientId))
            return Result<GooglePayload>.Failure(AuthErrors.GoogleTokenInvalid);

        try
        {
            // GoogleJsonWebSignature.ValidateAsync has no overload accepting a
            // CancellationToken, so the certificate fetch it performs cannot be cancelled.
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    // Pinning the audience is what stops a token minted for a different
                    // application from being replayed against this application.
                    Audience = [options.Value.ClientId]
                });

            return Result<GooglePayload>.Success(
                new GooglePayload(payload.Subject, payload.Email, payload.EmailVerified, payload.Name));
        }
        catch (InvalidJwtException)
        {
            return Result<GooglePayload>.Failure(AuthErrors.GoogleTokenInvalid);
        }
    }
}
