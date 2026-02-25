using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicTokenStatus;

public sealed class GetPublicTokenStatusQueryValidator : AbstractValidator<GetPublicTokenStatusQuery>
{
    public GetPublicTokenStatusQueryValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
