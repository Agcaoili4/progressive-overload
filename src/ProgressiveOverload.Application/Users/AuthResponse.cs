namespace ProgressiveOverload.Application.Users;

public sealed record AuthResponse(string AccessToken, Guid UserId, string DisplayName);

/*
    The raw refresh token is returned separately so the endpoint can place it in an
    httpOnly cookie. It must never appear in a JSON response body (spec §7).
*/
public sealed record AuthResult(AuthResponse Response, string RefreshTokenRaw);
