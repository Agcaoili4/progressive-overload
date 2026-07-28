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

        builder.Property(e => e.WeightKg).HasPrecision(7, 2).IsRequired();
        builder.Property(e => e.RecordedAt).IsRequired();

        // One user's entries, newest first — the order every weight chart and history
        // screen asks for. Ascending on UserId, descending on RecordedAt.
        builder.HasIndex(e => new { e.UserId, e.RecordedAt }).IsDescending(false, true);
    }
}
