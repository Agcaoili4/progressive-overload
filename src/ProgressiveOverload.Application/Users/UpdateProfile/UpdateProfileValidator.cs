using FluentValidation;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.UpdateProfile;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
        RuleFor(x => x.Bio).MaximumLength(500);
        RuleFor(x => x.Sex).IsInEnum().When(x => x.Sex.HasValue);
        RuleFor(x => x.ExperienceLevel).IsInEnum().When(x => x.ExperienceLevel.HasValue);
        RuleFor(x => x.Units).IsInEnum();
    }
}
