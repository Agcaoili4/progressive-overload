using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Persistence;
using Testcontainers.PostgreSql;

namespace ProgressiveOverload.Integration.Tests.Infrastructure;

/*
    Starts a throwaway PostgreSQL container for integration tests and applies the real
    migrations to it. Tests run against actual Postgres rather than EF's in-memory
    provider, because in-memory ignores unique indexes and other constraints — it would
    pass tests that production fails.
*/
public sealed class PostgresFixture : IAsyncLifetime
{
    // Separate from the docker-compose Postgres you run locally. Testcontainers starts
    // and destroys this one per test run, on a random port, so tests never collide with
    // your development data.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new AppDbContext(options);

        // MigrateAsync, not EnsureCreatedAsync. Running the real migration files means a
        // broken migration fails the test suite instead of surfacing during a deploy.
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

// Lets every test class marked [Collection(nameof(PostgresCollection))] share one
// container instead of starting a fresh one each time, which would be very slow.
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
