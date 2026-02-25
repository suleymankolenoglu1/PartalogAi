using FluentValidation;

namespace Katalogcu.Application.Features.Products.Queries.GetStockMovements;

public sealed class GetStockMovementsQueryValidator : AbstractValidator<GetStockMovementsQuery>
{
    public GetStockMovementsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 200)
            .WithMessage("Limit 1 ile 200 arasında olmalıdır.");
    }
}
