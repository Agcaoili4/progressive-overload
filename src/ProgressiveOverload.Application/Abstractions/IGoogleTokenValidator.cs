using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Application.Abstractions;

public sealed record GooglePayload(string Subject, string Email, bool EmailVerified, string? Name);

/*
    The port exists so the handler's linking logic can be tested with a fake, without
    network calls to Google.
*/
public interface IGoogleTokenValidator
{
    Task<Result<GooglePayload>> Validate(string idToken, CancellationToken ct);
}
