using FluentValidation;
using ProgressiveOverload.Api.Extensions;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Application.Users.RecordBodyweight;
using ProgressiveOverload.Application.Users.UpdateProfile;

namespace ProgressiveOverload.Api.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization on the group is the boundary that makes this the first
        // protected surface in the product: every handler behind it still reads the user
        // id from ICurrentUser rather than the request, but none of them work at all
        // without a validated token to begin with.
        var group = app.MapGroup("/api/v1/me").WithTags("Profile").RequireAuthorization();

        group.MapGet("/", async (GetProfileHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        group.MapPatch("/", async (
            UpdateProfileCommand command,
            IValidator<UpdateProfileCommand> validator,
            UpdateProfileHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        group.MapPost("/bodyweight", async (
            RecordBodyweightCommand command,
            IValidator<RecordBodyweightCommand> validator,
            RecordBodyweightHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/me/bodyweight/{result.Value.Id}", result.Value)
                : result.Error.ToProblem();
        });
    }
}
