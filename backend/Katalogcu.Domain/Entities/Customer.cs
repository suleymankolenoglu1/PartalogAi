using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities
{
    public class Customer : BaseEntity
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string NormalizedPhone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? CompanyName { get; set; }
        public string? Note { get; set; }
        public DateTime LastVisitDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastOrderDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public int OrderCount { get; set; } = 0;
        public decimal TotalSpent { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public string? LoginCode { get; set; }
        public DateTime? LoginCodeExpiresAt { get; set; }
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }
        public int FailedLoginCount { get; set; } = 0;
        public DateTime? LoginLockoutUntil { get; set; }
        public string? PublicSessionToken { get; set; }
        public DateTime? PublicSessionExpiresAt { get; set; }
    }
}
