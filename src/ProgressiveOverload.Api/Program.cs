using FluentValidation;
using ProgressiveOverload.Api.Endpoints;
using ProgressiveOverload.Application.Users.Login;
using ProgressiveOverload.Application.Users.Register;
using ProgressiveOverload.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddProblemDetails(options =>
{
    // The framework's default problem-details writer stamps a fresh Activity trace id
    // into every response body. Two calls to POST /login always differ by that trace id
    // alone, which would silently defeat the login endpoint's enumeration defence even
    // though status, title, and error code all match. Stripping it here applies to every
    // problem response, not just login, which is fine: a trace id belongs in logs, not in
    // a client-facing error body.
    options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions.Remove("traceId");
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapAuthEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
