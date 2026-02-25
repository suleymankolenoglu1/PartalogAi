using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicCatalogsByUser;

public sealed class GetPublicCatalogsByUserQueryValidator : AbstractValidator<GetPublicCatalogsByUserQuery>
{
    public GetPublicCatalogsByUserQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz token.");
    }
}
