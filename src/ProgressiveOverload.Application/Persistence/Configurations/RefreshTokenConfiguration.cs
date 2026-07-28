using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProgressiveOverload.Domain.Auth;

namespace ProgressiveOverload.Application.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        // 64 chars because we store a SHA-256 hash as hex, never the token itself.
        // Every refresh request looks the token up by this hash, hence the unique index.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // FamilyId is indexed because detecting a stolen token means revoking every token
        // in that family at once.
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);

        // IsActive is computed from RedeemedAt/RevokedAt, so it must not become a column.
        builder.Ignore(t => t.IsActive);
    }
}
