using Katalogcu.Domain.Common;
using Katalogcu.Domain.Enums;

namespace Katalogcu.Domain.Entities
{
    public class AppUser : BaseEntity
    {
        public string FirstName {get;set;} = string.Empty;
        public string LastName {get;set;} = string.Empty;
        public string Email {get;set;} = string.Empty;
        public string PasswordHash {get;set;} = string.Empty;
        public string? PasswordSalt { get; set; }
        public string Role {get;set;} = "Customer";
        public string? CompanyName {get; set;} 
        public string? PhoneNumber { get; set; }
        public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.CatalogOnly;
        public DateTime? PlanActivatedAt { get; set; }
        public DateTime? PlanExpiresAt { get; set; }
        public int MaxCatalogCount { get; set; } = 3;
        public int MaxPagePerCatalog { get; set; } = 100;
        public int PublicLinkVersion { get; set; } = 1;
        public bool PublicLinkEnabled { get; set; } = true;
    }
}
