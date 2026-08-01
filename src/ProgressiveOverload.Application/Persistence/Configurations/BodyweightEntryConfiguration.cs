using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence.Configurations;

public sealed class BodyweightEntryConfiguration : IEntityTypeConfiguration<BodyweightEntry>
{
    public void Configure(EntityTypeBuilder<BodyweightEntry> builder)
    {
        builder.ToTable("bodyweight_entries");
        builder.HasKey(e => e.Id);

        /*
            Ids are always assigned in C# via Guid.CreateVersion7(), never by the database.
            Left at EF's default ValueGeneratedOnAdd, two things go wrong: an entity built
            with a default Guid.Empty would get one filled in by EF's own client-side
            GuidValueGenerator, which produces a v4 GUID — silently violating this
            project's UUIDv7 rule — and EF's entity-state heuristic treats "key already
            set" as "row already exists in the database," so a new entry discovered only
            through a tracked parent's navigation gets an UPDATE instead of an INSERT and
            fails with a concurrency exception, as happened here.
        */
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.WeightKg).HasPrecision(7, 2).IsRequired();
        builder.Property(e => e.RecordedAt).IsRequired();

        // One user's entries, newest first — the order every weight chart and history
        // screen asks for. Ascending on UserId, descending on RecordedAt.
        builder.HasIndex(e => new { e.UserId, e.RecordedAt }).IsDescending(false, true);
    }
}
