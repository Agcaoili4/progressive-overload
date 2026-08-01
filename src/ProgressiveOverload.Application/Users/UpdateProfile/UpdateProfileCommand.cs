using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.UpdateProfile;

public sealed record UpdateProfileCommand(
    string DisplayName,
    string? Bio,
    Sex? Sex,
    ExperienceLevel? ExperienceLevel,
    UnitPreference Units);
