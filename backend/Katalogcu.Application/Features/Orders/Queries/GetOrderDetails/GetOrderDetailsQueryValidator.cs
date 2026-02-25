using FluentValidation;

namespace Katalogcu.Application.Features.Orders.Queries.GetOrderDetails;

public sealed class GetOrderDetailsQueryValidator : AbstractValidator<GetOrderDetailsQuery>
{
    public GetOrderDetailsQueryValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçerli bir sipariş seçiniz.");
    }
}
