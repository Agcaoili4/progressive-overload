using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.GetProfile;

public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    Sex? Sex,
    ExperienceLevel? ExperienceLevel,
    UnitPreference Units,
    decimal? CurrentBodyweightKg,
    DateTimeOffset CreatedAt);
