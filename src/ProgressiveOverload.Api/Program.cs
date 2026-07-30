using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

var builder = WebApplication.CreateBuilder(args);

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

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

        // Without this, ASP.NET's default inbound claim mapping rewrites "sub" to
        // ClaimTypes.NameIdentifier before code ever sees it. CurrentUser checks both
        // claim names, so auth still works either way, but leaving the mapping on means
        // the claim name in the token and the claim name in code silently disagree.
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
            // The default ClockSkew is five minutes, which would keep a 15-minute access
            // token alive for twenty. Real clock drift between servers needs seconds, not
            // minutes, so this is set explicitly rather than left at the default.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
