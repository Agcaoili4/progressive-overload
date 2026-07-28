using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(256);

        builder.Property(u => u.GoogleSubject).HasMaxLength(255);
        builder.HasIndex(u => u.GoogleSubject)
            .IsUnique()
            .HasFilter("google_subject IS NOT NULL");

        builder.Property(u => u.DisplayName).HasMaxLength(User.MaxDisplayNameLength).IsRequired();
        builder.Property(u => u.Bio).HasMaxLength(500);
        builder.Property(u => u.AvatarUrl).HasMaxLength(2048);

        builder.Property(u => u.Sex).HasConversion<int>();
        builder.Property(u => u.ExperienceLevel).HasConversion<int>();
        builder.Property(u => u.Units).HasConversion<int>().IsRequired();

        builder.Property(u => u.CurrentBodyweightKg).HasPrecision(7, 2);

        builder.HasMany(u => u.BodyweightEntries)
            .WithOne()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.BodyweightEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
