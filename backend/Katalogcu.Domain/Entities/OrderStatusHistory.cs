using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; set; }
    public int? PreviousStatus { get; set; }
    public int NewStatus { get; set; }
    public bool IsVisibleToCustomer { get; set; } = true;
    public string Source { get; set; } = "System";
    public string? Note { get; set; }
    public string? ChangedBy { get; set; }
}
