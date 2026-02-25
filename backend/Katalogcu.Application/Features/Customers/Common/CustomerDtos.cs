namespace Katalogcu.Application.Features.Customers.Common;

public sealed class CustomerListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Company { get; init; }
    public string? Email { get; init; }
    public string Phone { get; init; } = string.Empty;
    public int OrderCount { get; init; }
    public decimal TotalSpent { get; init; }
    public DateTime LastVisitDate { get; init; }
    public DateTime? LastOrderDate { get; init; }
    public string Status { get; init; } = "inactive";
    public string? Note { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class PublicCustomerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Company { get; init; }
    public DateTime? LastLoginDate { get; init; }
}

public sealed class PublicCustomerOrderSummaryDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public int Status { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime CreatedDate { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string DeliveryCity { get; init; } = string.Empty;
    public int ItemCount { get; init; }
}

public sealed class PublicCustomerOrderItemProductDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class PublicCustomerOrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public PublicCustomerOrderItemProductDto? Product { get; init; }
}

public sealed class PublicCustomerOrderDetailDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public int Status { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime CreatedDate { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;
    public string DeliveryCity { get; init; } = string.Empty;
    public string? DeliveryDistrict { get; init; }
    public string? DeliveryNote { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public IReadOnlyList<PublicCustomerOrderItemDto> Items { get; init; } = [];
    public IReadOnlyList<PublicCustomerOrderStatusHistoryDto> StatusHistory { get; init; } = [];
}

public sealed class PublicCustomerOrderStatusHistoryDto
{
    public Guid Id { get; init; }
    public int? PreviousStatus { get; init; }
    public int NewStatus { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string? ChangedBy { get; init; }
    public DateTime CreatedDate { get; init; }
}
