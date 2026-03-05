using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.TrackStorefrontView;

public sealed class TrackStorefrontViewCommandValidator : AbstractValidator<TrackStorefrontViewCommand>
{
    public TrackStorefrontViewCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEmpty();

        RuleFor(x => x.FingerprintHash)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Source)
            .NotEmpty()
            .MaximumLength(64);
    }
}
