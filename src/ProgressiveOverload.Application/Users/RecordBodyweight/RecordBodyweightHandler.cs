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

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user is null) return Result<BodyweightResponse>.Failure(UserErrors.NotFound);

        var recorded = user.RecordBodyweight(command.WeightKg, command.RecordedAt ?? clock.UtcNow);
        if (recorded.IsFailure) return Result<BodyweightResponse>.Failure(recorded.Error);

        /*
            Stated explicitly because the entry carries a client-assigned key. While the Guid
            keys still had EF's default ValueGeneratedOnAdd, reaching this row only through
            the User.BodyweightEntries navigation made EF treat it as an existing row and
            issue a zero-row UPDATE. ValueGeneratedNever fixed that; the Add keeps the insert
            correct regardless of how key generation is configured later.
        */
        db.BodyweightEntries.Add(recorded.Value);
        await db.SaveChangesAsync(ct);

        return Result<BodyweightResponse>.Success(new BodyweightResponse(
            recorded.Value.Id, recorded.Value.WeightKg, recorded.Value.RecordedAt));
    }
}
