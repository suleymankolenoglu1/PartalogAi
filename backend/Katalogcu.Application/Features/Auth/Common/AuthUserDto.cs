namespace Katalogcu.Application.Features.Auth.Common;

public sealed class AuthUserDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? PhoneNumber { get; init; }
    public string Role { get; init; } = "Customer";
    public int SubscriptionPlan { get; init; }
    public DateTime? PlanActivatedAt { get; init; }
    public DateTime? PlanExpiresAt { get; init; }
    public bool PlanSelected { get; init; }
    public int MaxCatalogCount { get; init; }
    public int MaxPagePerCatalog { get; init; }
}
