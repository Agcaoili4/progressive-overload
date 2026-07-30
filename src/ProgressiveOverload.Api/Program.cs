using FluentValidation;
using ProgressiveOverload.Api.Endpoints;
using ProgressiveOverload.Application.Users.GoogleSignIn;
using ProgressiveOverload.Application.Users.Login;
using ProgressiveOverload.Application.Users.Logout;
using ProgressiveOverload.Application.Users.Refresh;
using ProgressiveOverload.Application.Users.Register;
using ProgressiveOverload.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<GoogleSignInHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapAuthEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
