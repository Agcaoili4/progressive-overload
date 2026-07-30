using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.GetProfile;

public sealed class GetProfileHandler(AppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ProfileResponse>> Handle(CancellationToken ct)
    {
        // Identity comes only from the authenticated principal, never from a request
        // parameter — no endpoint in this codebase accepts a caller-supplied user id
        // (spec §7). Trusting one would let a caller read another user's profile.
        var userId = currentUser.UserId;
        if (userId is null) return Result<ProfileResponse>.Failure(UserErrors.NotFound);

        var profile = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new ProfileResponse(
                u.Id, u.Email, u.DisplayName, u.Bio, u.AvatarUrl,
                u.Sex, u.ExperienceLevel, u.Units, u.CurrentBodyweightKg, u.CreatedAt))
            .SingleOrDefaultAsync(ct);

        return profile is null
            ? Result<ProfileResponse>.Failure(UserErrors.NotFound)
            : Result<ProfileResponse>.Success(profile);
    }
}
