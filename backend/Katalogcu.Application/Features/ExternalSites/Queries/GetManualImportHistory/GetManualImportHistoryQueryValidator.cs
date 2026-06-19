using FluentValidation;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetManualImportHistory;

public sealed class GetManualImportHistoryQueryValidator : AbstractValidator<GetManualImportHistoryQuery>
{
    public GetManualImportHistoryQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
    }
}
