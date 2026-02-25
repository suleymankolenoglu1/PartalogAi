using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogStats;

public sealed class GetCatalogStatsQueryValidator : AbstractValidator<GetCatalogStatsQuery>
{
    public GetCatalogStatsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz kullanıcı.");
    }
}
