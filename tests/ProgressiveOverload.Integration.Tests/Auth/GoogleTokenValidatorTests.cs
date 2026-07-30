using Microsoft.Extensions.Options;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Infrastructure.Auth;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

/*
    No PostgresCollection fixture and no [Collection] here on purpose: both cases below are
    decided before any database or network call happens, so this class needs neither.
*/
public sealed class GoogleTokenValidatorTests
{
    [Fact]
    public async Task Validate_FailsClosed_WhenClientIdIsNotConfigured()
    {
        // The token is an empty string rather than an arbitrary placeholder like
        // "anything". Google.Apis.Auth rejects a plain malformed string with
        // InvalidJwtException regardless of ClientId, which this code also catches and
        // maps to the same failure - so a placeholder string would pass this test whether
        // or not the guard below actually runs, proving nothing. An empty string instead
        // makes Google.Apis.Auth throw ArgumentException, which this code does NOT catch:
        // if the guard were ever removed, this test would fail with an unhandled
        // exception instead of quietly passing for the wrong reason.
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions()));

        var result = await validator.Validate(string.Empty, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.GoogleTokenInvalid);
    }

    [Fact]
    public async Task Validate_RejectsMalformedToken_WithoutReachingTheNetwork()
    {
        // A non-empty ClientId here so the empty-ClientId guard is not what causes this
        // failure. "not-a-valid-jwt" fails to parse as a JWT, which throws
        // InvalidJwtException before any certificate fetch is attempted - a well-formed
        // token is deliberately not exercised here, since that would reach Google's
        // network and make this test flaky and externally dependent.
        var validator = new GoogleTokenValidator(
            Options.Create(new GoogleAuthOptions { ClientId = "some-client-id" }));

        var result = await validator.Validate("not-a-valid-jwt", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.GoogleTokenInvalid);
    }
}
