using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.UpdateProfile;

public sealed class UpdateProfileHandler(AppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ProfileResponse>> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        // Identity comes only from the authenticated principal, never from the command
        // body — a caller-supplied id here would let one user overwrite another's profile.
        var userId = currentUser.UserId;
        if (userId is null) return Result<ProfileResponse>.Failure(UserErrors.NotFound);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null) return Result<ProfileResponse>.Failure(UserErrors.NotFound);

        var update = user.UpdateProfile(
            command.DisplayName, command.Bio, command.Sex, command.ExperienceLevel, command.Units);

        if (update.IsFailure) return Result<ProfileResponse>.Failure(update.Error);

        await db.SaveChangesAsync(ct);

        return Result<ProfileResponse>.Success(new ProfileResponse(
            user.Id, user.Email, user.DisplayName, user.Bio, user.AvatarUrl,
            user.Sex, user.ExperienceLevel, user.Units, user.CurrentBodyweightKg, user.CreatedAt));
    }
}
