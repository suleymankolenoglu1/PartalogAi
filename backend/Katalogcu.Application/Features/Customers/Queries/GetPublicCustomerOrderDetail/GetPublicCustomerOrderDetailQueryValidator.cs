using FluentValidation;

namespace Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrderDetail;

public sealed class GetPublicCustomerOrderDetailQueryValidator : AbstractValidator<GetPublicCustomerOrderDetailQuery>
{
    public GetPublicCustomerOrderDetailQueryValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz public link.");

        RuleFor(x => x.SessionToken)
            .NotEmpty()
            .WithMessage("Oturum geçersiz.");

        RuleFor(x => x.OrderId)
            .NotEqual(Guid.Empty)
            .WithMessage("Sipariş bulunamadı.");
    }
}
