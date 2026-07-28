using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Application.Persistence;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Infrastructure;

[Collection(nameof(PostgresCollection))]
public sealed class SchemaTests(PostgresFixture fixture)
{
    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task UserRoundTripsWithBodyweightHistory()
    {
        var user = User.CreateWithPassword($"{Guid.NewGuid():N}@example.com", "hash", "Jansen").Value;
        user.RecordBodyweight(84.5m, DateTimeOffset.UtcNow);

        await using (var db = NewContext())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await db.Users
                .Include(u => u.BodyweightEntries)
                .SingleAsync(u => u.Id == user.Id);

            loaded.CurrentBodyweightKg.ShouldBe(84.5m);
            loaded.BodyweightEntries.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task DuplicateEmailIsRejectedByTheDatabase()
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        await using var db = NewContext();
        db.Users.Add(User.CreateWithPassword(email, "hash", "One").Value);
        await db.SaveChangesAsync();

        db.Users.Add(User.CreateWithPassword(email, "hash", "Two").Value);
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
