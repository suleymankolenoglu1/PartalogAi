using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using Katalogcu.Domain.Enums;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicStorefront;

public sealed class GetPublicStorefrontQueryHandler : IRequestHandler<GetPublicStorefrontQuery, OperationResult<PublicStorefrontDto>>
{
    private readonly IUserRepository _userRepository;

    public GetPublicStorefrontQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<OperationResult<PublicStorefrontDto>> Handle(GetPublicStorefrontQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return OperationResult<PublicStorefrontDto>.Failure("not_found", "İşletme bulunamadı.");
        }

        var ownerName = $"{user.FirstName} {user.LastName}".Trim();
        var businessName = !string.IsNullOrWhiteSpace(user.CompanyName)
            ? user.CompanyName.Trim()
            : (!string.IsNullOrWhiteSpace(ownerName) ? ownerName : "Katalog Magazasi");
        var plan = user.SubscriptionPlan;

        return OperationResult<PublicStorefrontDto>.Success(new PublicStorefrontDto
        {
            BusinessName = businessName,
            OwnerName = ownerName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            SubscriptionPlan = (int)plan,
            AiChatEnabled = plan is SubscriptionPlan.CatalogWithAI or SubscriptionPlan.CatalogWithAIAndEcommerce,
            EcommerceEnabled = plan == SubscriptionPlan.CatalogWithAIAndEcommerce
        });
    }
}
