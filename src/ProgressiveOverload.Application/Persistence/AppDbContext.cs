using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence;

/// <summary>
/// The database. Lives in Application rather than Infrastructure on purpose: this codebase
/// does not hide EF behind a repository layer, so feature handlers use this class directly.
/// Putting it in Infrastructure would make Application depend on Infrastructure, which
/// already depends on Application — a circular reference that will not compile.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BodyweightEntry> BodyweightEntries => Set<BodyweightEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in this project, so adding a new entity
        // means adding one configuration file — nothing to register here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
