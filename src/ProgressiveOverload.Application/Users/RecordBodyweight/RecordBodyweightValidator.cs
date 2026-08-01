using FluentValidation;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.RecordBodyweight;

public sealed class RecordBodyweightValidator : AbstractValidator<RecordBodyweightCommand>
{
    public RecordBodyweightValidator()
    {
        RuleFor(x => x.WeightKg)
            .InclusiveBetween(User.MinBodyweightKg, User.MaxBodyweightKg);
    }
}
