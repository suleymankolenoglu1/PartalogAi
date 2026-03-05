using System.ComponentModel.DataAnnotations.Schema;
using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class PlatformAuditLog : BaseEntity
{
    public Guid? ActorUserId { get; set; }
    public Guid? TargetOwnerUserId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    [Column(TypeName = "jsonb")]
    public string? Payload { get; set; }
}
