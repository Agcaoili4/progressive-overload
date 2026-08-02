using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProgressiveOverload.Api.Endpoints;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Application.Users.GoogleSignIn;
using ProgressiveOverload.Application.Users.Login;
using ProgressiveOverload.Application.Users.Logout;
using ProgressiveOverload.Application.Users.RecordBodyweight;
using ProgressiveOverload.Application.Users.Refresh;
using ProgressiveOverload.Application.Users.Register;
using ProgressiveOverload.Application.Users.UpdateProfile;
using ProgressiveOverload.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"] ?? string.Empty;
    options.Environment = builder.Environment.EnvironmentName;
    options.Release = builder.Configuration["Sentry:Release"];
    options.TracesSampleRate = 0.1;

    /*
        The app handles email addresses and bodyweight — health-adjacent personal data that
        must never reach a third-party error tracker (spec §11). Request bodies are dropped
        wholesale rather than filtered, because /auth/login and /auth/register carry
        plaintext passwords.
    */
    options.SendDefaultPii = false;
    options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;

    // Fixed rather than the default machine name, which SendDefaultPii does not suppress and
    // which on a developer machine carries a person's name into every event.
    options.ServerName = "progressive-overload-api";

    /*
        Development and Testing drop every event, so local noise and test runs never reach
        the project. Sentry:SendInDevelopment opts one local run back in, which is the only
        way to confirm the two scrubbing rules above actually hold — an unverified claim
        about what reaches a third party is worth little. Defaults to false; never set it
        in a deployed environment.
    */
    var sendFromLocal = builder.Configuration.GetValue<bool>("Sentry:SendInDevelopment");
    var suppressLocally = !sendFromLocal
        && (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));

    options.Debug = sendFromLocal;

    /*
        Both hooks, not just the first: transactions carry the same request context as events
        and are not passed through SetBeforeSend, so suppressing only events would still ship
        local traces to the project. ASP.NET also fills request.env.SERVER_NAME with the
        machine name, which options.ServerName above does not govern.
    */
    options.SetBeforeSend((@event, _) =>
    {
        if (suppressLocally) return null;
        @event.Request.Env.Remove("SERVER_NAME");
        return @event;
    });

    options.SetBeforeSendTransaction((transaction, _) =>
    {
        if (suppressLocally) return null;
        transaction.Request.Env.Remove("SERVER_NAME");
        return transaction;
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<GoogleSignInHandler>();
builder.Services.AddScoped<GetProfileHandler>();
builder.Services.AddScoped<UpdateProfileHandler>();
builder.Services.AddScoped<RecordBodyweightHandler>();
builder.Services.AddProblemDetails();

/*
    Origins come from configuration and are always explicit — never AllowAnyOrigin. The web
    client sends the refresh cookie, which requires AllowCredentials, and a browser rejects
    a wildcard origin on a credentialed request outright. Configuring both also throws at
    startup, so the two can never be combined by accident.

    An empty list is the correct default rather than a permissive one: with no origins the
    policy matches nothing, browsers are refused, and non-browser callers are unaffected.
    Production supplies its origin by environment variable, so no deployed host is baked in.
*/
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowCredentials()
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

/*
    Configured through DI rather than inside AddJwtBearer so it reads the same
    ValidateOnStart-guarded JwtOptions that AddInfrastructure registers. Binding the section
    a second time here would bypass that validation, and its null-forgiving `!` would become
    a boot-time NullReferenceException if the section were ever renamed.
*/
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        /*
            Without this, ASP.NET's default inbound claim mapping rewrites "sub" to
            ClaimTypes.NameIdentifier before code ever sees it. CurrentUser checks both
            claim names, so auth still works either way, but leaving the mapping on means
            the claim name in the token and the claim name in code silently disagree.
        */
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            /*
                The default ClockSkew is five minutes, which would keep a 15-minute access
                token alive for twenty. Real clock drift between servers needs seconds, not
                minutes, so this is set explicitly rather than left at the default.
            */
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    /*
        Partitioned per IP. Correct for a single Render instance (spec §10); revisit if the
        API is ever scaled out, since in-memory partitions are per-process. Behind a proxy
        RemoteIpAddress is the proxy unless forwarded headers are configured, which would
        collapse every caller into one partition — see the note on UseForwardedHeaders.
    */
    options.AddPolicy(AuthEndpoints.StrictAuthPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

/*
    Must run before the rate limiter, which partitions on RemoteIpAddress. Behind Render's
    proxy that is the proxy's own address, so without this every caller shares one partition
    and the strict-auth limit becomes a global cap rather than a per-client one.

    ForwardLimit = 1 is the security control, not a tuning knob. The middleware reads
    X-Forwarded-For right to left, and the proxy appends the true client address as the last
    entry, so the rightmost entry is the only one a client cannot forge. Raising this limit
    walks left into values the caller supplied and hands them the ability to evade the
    limiter by rotating a header. The known-proxy lists are cleared because Render's egress
    addresses are not fixed; that is safe only in combination with the limit of one.
*/
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor,
    ForwardLimit = 1
};

// Cleared, not initialised empty: these default to loopback only, and a collection
// initializer would add nothing and silently leave that default in place.
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeaders);

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

/*
    Ahead of UseAuthorization specifically. The 401 challenge for a protected endpoint is
    raised there, and anything emitted upstream of UseCors reaches the browser without the
    CORS header — which the browser reports as an opaque CORS failure rather than a status,
    so an expired session would surface to the user as "network error" instead of "signed
    out". Placing it merely before UseAuthentication is not enough, and endpoint responses
    are unaffected wherever it sits, so the ordering looks harmless until it is measured.

    Preflights consume no strict-auth permit at any position: OPTIONS never matches the
    MapPost endpoint, so RequireRateLimiting metadata does not apply to them.
*/
app.UseCors();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
