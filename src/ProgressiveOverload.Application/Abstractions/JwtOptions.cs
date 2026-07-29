namespace ProgressiveOverload.Application.Abstractions;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "progressiveoverload";
    public string Audience { get; init; } = "progressiveoverload";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}
