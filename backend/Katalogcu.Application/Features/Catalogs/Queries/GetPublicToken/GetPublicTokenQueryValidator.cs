using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicToken;

public sealed class GetPublicTokenQueryValidator : AbstractValidator<GetPublicTokenQuery>
{
    public GetPublicTokenQueryValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
