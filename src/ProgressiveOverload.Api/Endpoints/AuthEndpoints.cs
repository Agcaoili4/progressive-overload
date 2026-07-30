using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using ProgressiveOverload.Api.Extensions;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Persistence.Configurations;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Application.Users.GoogleSignIn;
using ProgressiveOverload.Application.Users.Login;
using ProgressiveOverload.Application.Users.Logout;
using ProgressiveOverload.Application.Users.Refresh;
using ProgressiveOverload.Application.Users.Register;
using ProgressiveOverload.Domain.Auth;
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
            catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
            {
                // Lost the race on the unique email index. The `when` filter excludes
                // other DbUpdateException causes (FK violation, check-constraint failure,
                // transient connection fault), which propagate as a real 500 instead of
                // getting disguised as this 409.
                return UserErrors.EmailAlreadyRegistered.ToProblem();
            }

            if (result.IsFailure) return result.Error.ToProblem();

            http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
            return Results.Created($"/api/v1/users/{result.Value.Response.UserId}", result.Value.Response);
        })
        .AllowAnonymous();

        group.MapPost("/login", async (
            LoginCommand command,
            IValidator<LoginCommand> validator,
            LoginHandler handler,
            IOptions<JwtOptions> jwtOptions,
            HttpContext http,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(command, ct);
            if (result.IsFailure) return result.Error.ToProblem();

            http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
            return Results.Ok(result.Value.Response);
        })
        .AllowAnonymous();

        group.MapPost("/refresh", async (
            RefreshHandler handler,
            IOptions<JwtOptions> jwtOptions,
            HttpContext http,
            CancellationToken ct) =>
        {
            var raw = http.Request.Cookies[RefreshCookieName];
            if (string.IsNullOrWhiteSpace(raw))
                return AuthErrors.RefreshTokenInvalid.ToProblem();

            var result = await handler.Handle(raw, ct);
            if (result.IsFailure)
            {
                // A failed refresh (bad, expired, revoked, or reused token) leaves a cookie
                // that can never work again, so clear it here instead of leaving the
                // client to hang onto a dead token.
                http.ClearRefreshCookie();
                return result.Error.ToProblem();
            }

            http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
            return Results.Ok(result.Value.Response);
        })
        .AllowAnonymous();

        group.MapPost("/logout", async (LogoutHandler handler, HttpContext http, CancellationToken ct) =>
        {
            await handler.Handle(http.Request.Cookies[RefreshCookieName], ct);
            http.ClearRefreshCookie();
            return Results.NoContent();
        })
        .AllowAnonymous();

        group.MapPost("/google", async (
            GoogleSignInCommand command,
            GoogleSignInHandler handler,
            IOptions<JwtOptions> jwtOptions,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(command.IdToken))
                return AuthErrors.GoogleTokenInvalid.ToProblem();

            var result = await handler.Handle(command, ct);
            if (result.IsFailure) return result.Error.ToProblem();

            http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
            return Results.Ok(result.Value.Response);
        })
        .AllowAnonymous();
    }

    /*
        Narrowly matches a unique violation on the users-email index, rather than trusting
        "DbUpdateException" alone to mean "duplicate email". SqlState 23505 is Postgres's
        unique-violation code; the constraint-name check rules out the also-unique
        google_subject index or any future unique constraint on this table.
    */
    private static bool IsUniqueEmailViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName == UserConfiguration.EmailUniqueIndexName;

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

    /*
        These options must exactly mirror SetRefreshCookie above or logout silently fails.
        Deleting a cookie means telling the browser to overwrite it with an
        already-expired one, which only works if the browser recognizes it as the SAME
        cookie - keyed by name + Path (and Domain), plus matching HttpOnly / Secure /
        SameSite. Any mismatch leaves the original cookie in place: the client believes
        it logged out while the browser keeps sending the old token.
    */
    public static void ClearRefreshCookie(this HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth"
        });
}
