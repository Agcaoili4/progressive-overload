using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Application.Persistence.Configurations;
using ProgressiveOverload.Domain.Users;
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

    /*
        AuthEndpoints.IsUniqueEmailViolation matches a Postgres unique-violation exception
        against this exact index name to turn a duplicate-email race into a 409. Nothing in
        the C# compiler catches a rename of the real database index, so this queries the
        live catalog rather than trusting the EF model - a rename here would silently
        degrade that 409 into an unhandled 500.
    */
    [Fact]
    public async Task EmailUniqueIndexIsNamedAsTheDuplicateDetectionExpects()
    {
        await using var db = NewContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select i.indisunique, array_agg(a.attname::text order by a.attnum) as columns
            from pg_index i
            join pg_class ix on ix.oid = i.indexrelid
            join pg_class t on t.oid = i.indrelid
            join pg_attribute a on a.attrelid = t.oid and a.attnum = any(i.indkey)
            where t.relname = 'users' and ix.relname = @indexName
            group by i.indisunique
            """;

        var indexNameParameter = command.CreateParameter();
        indexNameParameter.ParameterName = "indexName";
        indexNameParameter.Value = UserConfiguration.EmailUniqueIndexName;
        command.Parameters.Add(indexNameParameter);

        await using var reader = await command.ExecuteReaderAsync();
        var found = await reader.ReadAsync();

        found.ShouldBeTrue("no index named UserConfiguration.EmailUniqueIndexName exists on users");
        reader.GetBoolean(0).ShouldBeTrue("index exists but is not unique");
        ((string[])reader.GetValue(1)).ShouldContain("email");
    }
}
