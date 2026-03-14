using FluentValidation;

namespace Katalogcu.Application.Features.Orders.Queries.ResolveCartItemQuote;

public sealed class ResolveCartItemQuoteQueryValidator : AbstractValidator<ResolveCartItemQuoteQuery>
{
    public ResolveCartItemQuoteQueryValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(999);
    }
}
