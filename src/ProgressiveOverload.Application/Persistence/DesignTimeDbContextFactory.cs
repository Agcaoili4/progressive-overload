using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProgressiveOverload.Application.Persistence;

/*
    Used only by the `dotnet ef` command line when generating migrations. The app at runtime
    builds its DbContext from configuration instead, so this connection string is a local
    development convenience and not a secret.
*/
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5434;Database=progressiveoverload;Username=po;Password=localdev")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
