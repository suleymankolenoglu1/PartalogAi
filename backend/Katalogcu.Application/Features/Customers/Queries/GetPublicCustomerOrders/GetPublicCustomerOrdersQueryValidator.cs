using FluentValidation;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrders;

public sealed class GetPublicCustomerOrdersQueryValidator : AbstractValidator<GetPublicCustomerOrdersQuery>
{
    public GetPublicCustomerOrdersQueryValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz public link.");

        RuleFor(x => x.SessionToken)
            .NotEmpty()
            .WithMessage("Oturum geçersiz.");
    }
}
