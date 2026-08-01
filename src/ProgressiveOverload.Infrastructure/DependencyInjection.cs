using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Infrastructure.Auth;
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

        // A missing or too-short signing key must crash the process at startup rather than
        // surface as a 500 on the first login attempt. Without ValidateOnStart, a deployment
        // missing Jwt:SigningKey looks healthy — health checks pass, the process stays up —
        // right up until the first user tries to log in and JwtTokenService throws. Failing
        // loudly at boot turns a silent, hard-to-diagnose production incident into an
        // immediate, obvious deployment failure.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey is required.")
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                      "Jwt:SigningKey must be at least 32 bytes for HS256.")
            .Validate(o => o.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be positive.")
            .Validate(o => o.RefreshTokenDays > 0, "Jwt:RefreshTokenDays must be positive.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // No ValidateOnStart here, unlike JwtOptions above: Google sign-in must stay
        // optional so the app still boots and serves email/password auth when it is not
        // configured. GoogleTokenValidator fails closed itself when ClientId is blank.
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

        return services;
    }
}
