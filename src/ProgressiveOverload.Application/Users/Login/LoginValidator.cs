using FluentValidation;

namespace ProgressiveOverload.Application.Users.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);

        // Deliberately no MinimumLength rule here, unlike RegisterValidator. Enforcing
        // today's minimum on login would reject an existing user whose password predates
        // a policy change, and the rejection itself would leak the current policy to an
        // attacker probing this endpoint.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}
