using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class ManualImportFile : BaseEntity
{
    public Guid ExternalSiteId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; }
    public Guid ImportedByUserId { get; set; }
    public int RowCount { get; set; }
    public string Status { get; set; } = "queued";
    public string? ErrorSummary { get; set; }

    public ExternalSite? ExternalSite { get; set; }
    public AppUser? ImportedByUser { get; set; }
}
