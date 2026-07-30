using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /* AuthEndpoints.IsUniqueEmailViolation matches the Postgres constraint name against this
       exact string to turn a duplicate-email race into a 409. Named explicitly, rather than
       left to the snake_case convention, so a rename here is a compile-time change at both
       sites instead of a silent divergence. */
    public const string EmailUniqueIndexName = "ix_users_email";

    /* Same reasoning as EmailUniqueIndexName above: AuthEndpoints matches this exact string
       against the Postgres constraint name to detect a concurrent-Google-sign-in race on the
       filtered google_subject index. */
    public const string GoogleSubjectUniqueIndexName = "ix_users_google_subject";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(EmailUniqueIndexName);

        builder.Property(u => u.PasswordHash).HasMaxLength(256);

        // Most users never link Google, so this index only covers the rows that did.
        // Without the filter it would also index every NULL and grow for no reason.
        builder.Property(u => u.GoogleSubject).HasMaxLength(255);
        builder.HasIndex(u => u.GoogleSubject)
            .IsUnique()
            .HasDatabaseName(GoogleSubjectUniqueIndexName)
            .HasFilter("google_subject IS NOT NULL");

        builder.Property(u => u.DisplayName).HasMaxLength(User.MaxDisplayNameLength).IsRequired();
        builder.Property(u => u.Bio).HasMaxLength(500);
        builder.Property(u => u.AvatarUrl).HasMaxLength(2048);

        // Store enums as their numeric values, not their names. The enums declare explicit
        // numbers so renaming or reordering a member can never change existing rows.
        builder.Property(u => u.Sex).HasConversion<int>();
        builder.Property(u => u.ExperienceLevel).HasConversion<int>();
        builder.Property(u => u.Units).HasConversion<int>().IsRequired();

        // decimal(7,2), never a floating-point column: rounding drift would make
        // personal records flicker and leaderboard positions unexplainable.
        builder.Property(u => u.CurrentBodyweightKg).HasPrecision(7, 2);

        builder.HasMany(u => u.BodyweightEntries)
            .WithOne()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // BodyweightEntries is a read-only view over a private List. Tell EF to write the
        // field directly, otherwise it tries to use the property setter, which doesn't exist.
        builder.Navigation(u => u.BodyweightEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
