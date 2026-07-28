using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BodyweightEntry> BodyweightEntries => Set<BodyweightEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
