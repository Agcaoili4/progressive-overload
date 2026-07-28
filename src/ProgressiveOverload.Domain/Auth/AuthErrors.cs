using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Domain.Auth;

public static class AuthErrors
{
    // "Email or password is incorrect" covers both a wrong password and an unknown
    // email on purpose. Two different messages would let someone probe the login form
    // to find out which addresses are registered.
    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Email or password is incorrect.");

    // Same idea below: the three refresh failures share one message so the client
    // learns nothing, but keep separate codes so our logs can tell them apart.
    public static readonly Error RefreshTokenInvalid =
        new("auth.refresh_token_invalid", "Your session has expired. Please sign in again.");

    public static readonly Error RefreshTokenExpired =
        new("auth.refresh_token_expired", "Your session has expired. Please sign in again.");

    public static readonly Error RefreshTokenReused =
        new("auth.refresh_token_reused", "Your session has expired. Please sign in again.");

    public static readonly Error GoogleTokenInvalid =
        new("auth.google_token_invalid", "Google sign-in failed. Please try again.");

    public static readonly Error GoogleEmailUnverified =
        new("auth.google_email_unverified", "Your Google email address is not verified.");
}
