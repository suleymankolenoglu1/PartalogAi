using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogAiJobs;

public sealed class GetCatalogAiJobsQueryValidator : AbstractValidator<GetCatalogAiJobsQuery>
{
    public GetCatalogAiJobsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty).WithMessage("Geçersiz kullanıcı.");
        RuleFor(x => x.Take).InclusiveBetween(1, 200).WithMessage("Take değeri 1 ile 200 arasında olmalı.");
    }
}
