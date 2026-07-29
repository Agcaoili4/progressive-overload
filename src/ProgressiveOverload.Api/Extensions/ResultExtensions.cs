using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Api.Extensions;

public static class ResultExtensions
{
    private static readonly Dictionary<string, int> StatusByErrorCode = new()
    {
        ["users.email_already_registered"] = StatusCodes.Status409Conflict,
        ["users.google_already_linked"] = StatusCodes.Status409Conflict,
        ["users.not_found"] = StatusCodes.Status404NotFound,
        ["auth.invalid_credentials"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_invalid"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_expired"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_reused"] = StatusCodes.Status401Unauthorized,
        ["auth.google_token_invalid"] = StatusCodes.Status401Unauthorized,
        ["auth.google_email_unverified"] = StatusCodes.Status403Forbidden
    };

    public static IResult ToProblem(this Error error)
    {
        var status = StatusByErrorCode.TryGetValue(error.Code, out var mapped)
            ? mapped
            : StatusCodes.Status400BadRequest;

        return Results.Problem(
            title: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
