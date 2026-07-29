using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Api.Extensions;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Application.Users.Register;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Api.Endpoints;

public static class AuthEndpoints
{
    public const string RefreshCookieName = "po_refresh";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterCommand command,
            IValidator<RegisterCommand> validator,
            RegisterHandler handler,
            IOptions<JwtOptions> jwtOptions,
            HttpContext http,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            Result<AuthResult> result;
            try
            {
                result = await handler.Handle(command, ct);
            }
            catch (DbUpdateException)
            {
                // Lost the race on the unique email index.
                return UserErrors.EmailAlreadyRegistered.ToProblem();
            }

            if (result.IsFailure) return result.Error.ToProblem();

            http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
            return Results.Created($"/api/v1/users/{result.Value.Response.UserId}", result.Value.Response);
        })
        .AllowAnonymous();
    }

    public static void SetRefreshCookie(this HttpContext http, string raw, int days) =>
        http.Response.Cookies.Append(RefreshCookieName, raw, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            // Scoped to the auth path so the cookie is not attached to unrelated API
            // calls and cannot be read by JavaScript (HttpOnly). SameSite=Strict is
            // viable because the API is served same-site with the web app.
            Path = "/api/v1/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            IsEssential = true
        });
}
