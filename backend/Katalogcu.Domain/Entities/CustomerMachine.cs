using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities;

public sealed class CustomerMachine : BaseEntity
{
    public Guid CustomerId { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Variant { get; set; }
    public string? MachineGroup { get; set; }
    public string? SerialNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public Customer? Customer { get; set; }
}
