using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities
{
    public class StockMovement : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid ProductId { get; set; }

        // Snapshot fields: ürün adı/kodu sonradan değişse de hareket kaydı anlamlı kalsın.
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;

        public int PreviousQuantity { get; set; }
        public int DeltaQuantity { get; set; }
        public int NewQuantity { get; set; }

        // ADJUSTMENT, IMPORT, ORDER vb.
        public string MovementType { get; set; } = "ADJUSTMENT";
        public string Reason { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? ActorName { get; set; }
        public string? ReferenceId { get; set; }

        public Product? Product { get; set; }
    }
}
