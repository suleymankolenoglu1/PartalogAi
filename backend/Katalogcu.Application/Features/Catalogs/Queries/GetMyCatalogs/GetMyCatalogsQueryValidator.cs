using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetMyCatalogs;

public sealed class GetMyCatalogsQueryValidator : AbstractValidator<GetMyCatalogsQuery>
{
    public GetMyCatalogsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz kullanıcı.");
    }
}
