using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class ExternalProductLinkCheck : BaseEntity
{
    public Guid ExternalProductId { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public string Method { get; set; } = "HEAD";
    public int? StatusCode { get; set; }
    public bool IsReachable { get; set; }
    public string? FinalUrl { get; set; }
    public string? ErrorSummary { get; set; }

    public ExternalProduct? ExternalProduct { get; set; }
}
