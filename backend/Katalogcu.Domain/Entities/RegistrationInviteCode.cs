using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities
{
    public class RegistrationInviteCode : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public string? UsedByEmail { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedByEmail { get; set; }
        public string? RevokedReason { get; set; }
        public string? CreatedByEmail { get; set; }
    }
}
