using FluentValidation;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public const int MinPasswordLength = 12;

    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);

        // NIST SP 800-63B: enforce length, drop composition rules. Requiring a symbol
        // and a digit produces "Password1!" and nothing else.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinPasswordLength)
            .WithMessage($"Password must be at least {MinPasswordLength} characters.")
            .MaximumLength(256);

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
    }
}
