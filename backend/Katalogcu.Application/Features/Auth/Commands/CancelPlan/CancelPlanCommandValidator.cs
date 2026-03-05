using FluentValidation;

namespace Katalogcu.Application.Features.Auth.Commands.CancelPlan;

public sealed class CancelPlanCommandValidator : AbstractValidator<CancelPlanCommand>
{
    public CancelPlanCommandValidator()
    {
    }
}
