using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicStorefront;

public sealed class GetPublicStorefrontQueryValidator : AbstractValidator<GetPublicStorefrontQuery>
{
    public GetPublicStorefrontQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz token.");
    }
}
