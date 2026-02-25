using FluentValidation;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerMe;

public sealed class GetPublicCustomerMeQueryValidator : AbstractValidator<GetPublicCustomerMeQuery>
{
    public GetPublicCustomerMeQueryValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz public link.");

        RuleFor(x => x.SessionToken)
            .NotEmpty()
            .WithMessage("Oturum geçersiz.");
    }
}
