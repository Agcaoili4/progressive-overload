using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Infrastructure.Auth;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Infrastructure.Time;

namespace ProgressiveOverload.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres"))
                   .UseSnakeCaseNamingConvention());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
