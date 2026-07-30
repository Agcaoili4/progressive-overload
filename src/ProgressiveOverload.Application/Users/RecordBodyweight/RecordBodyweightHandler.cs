using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.RecordBodyweight;

public sealed class RecordBodyweightHandler(AppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<Result<BodyweightResponse>> Handle(RecordBodyweightCommand command, CancellationToken ct)
    {
        // Identity comes only from the authenticated principal, never from the command
        // body — a caller-supplied id here would let one user log weight against another.
        var userId = currentUser.UserId;
        if (userId is null) return Result<BodyweightResponse>.Failure(UserErrors.NotFound);

        var user = await db.Users
            .Include(u => u.BodyweightEntries)
            .SingleOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user is null) return Result<BodyweightResponse>.Failure(UserErrors.NotFound);

        var recorded = user.RecordBodyweight(command.WeightKg, command.RecordedAt ?? clock.UtcNow);
        if (recorded.IsFailure) return Result<BodyweightResponse>.Failure(recorded.Error);

        // The new entry carries a client-assigned GUID, so without an explicit Add, EF
        // finds it only through the already-tracked User.BodyweightEntries navigation and
        // treats it as an existing row to UPDATE rather than a new one to INSERT — that
        // update matches zero rows and throws DbUpdateConcurrencyException.
        db.BodyweightEntries.Add(recorded.Value);
        await db.SaveChangesAsync(ct);

        return Result<BodyweightResponse>.Success(new BodyweightResponse(
            recorded.Value.Id, recorded.Value.WeightKg, recorded.Value.RecordedAt));
    }
}
