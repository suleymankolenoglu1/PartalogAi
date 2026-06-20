using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class ExternalProductOemNumber : BaseEntity
{
    public Guid ExternalProductId { get; set; }
    public string NormalizedOemNumber { get; set; } = string.Empty;
    public string OriginalOemNumber { get; set; } = string.Empty;

    public ExternalProduct? ExternalProduct { get; set; }
}
