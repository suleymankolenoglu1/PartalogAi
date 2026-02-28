namespace Katalogcu.Application.Features.Catalogs.Common;

public sealed class PublicStorefrontDto
{
    public string BusinessName { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public int SubscriptionPlan { get; init; }
    public bool AiChatEnabled { get; init; }
    public bool EcommerceEnabled { get; init; }
}
