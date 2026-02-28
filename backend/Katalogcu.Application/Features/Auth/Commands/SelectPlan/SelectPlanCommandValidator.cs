using FluentValidation;

namespace Katalogcu.Application.Features.Auth.Commands.SelectPlan;

public sealed class SelectPlanCommandValidator : AbstractValidator<SelectPlanCommand>
{
    public SelectPlanCommandValidator()
    {
        RuleFor(x => x.Plan)
            .InclusiveBetween(1, 3)
            .WithMessage("Geçersiz plan seçimi.");
    }
}
