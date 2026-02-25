using System;
using System.Collections.Generic;
using Katalogcu.Domain.Common;
namespace Katalogcu.Domain.Entities
{
    public class Order : BaseEntity // BaseEntity'de Id, CreatedDate var varsayıyorum
    {
        // Sipariş Takip Kodu (Örn: SP-2026-X92)
        public string OrderNumber { get; set; } =   string.Empty;
        
        // Müşteri Bilgileri (Giriş yapmadan alıyorsak)
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CompanyName { get; set; } // Opsiyonel: Firma adı
        public Guid? CustomerId { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DeliveryCity { get; set; } = string.Empty;
        public string? DeliveryDistrict { get; set; }
        public string? DeliveryNote { get; set; }
        public string PaymentMethod { get; set; } = "KapidaOdeme";
        public string? IdempotencyKey { get; set; }
        public Guid? OwnerUserId { get; set; }

        // Finansal
        public decimal TotalAmount { get; set; }
        
        // Durum (0: Bekliyor, 1: Onaylandı, 2: Kargoda vs.)
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        // İlişki: Bir siparişin birden çok kalemi olur
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    }

    public enum OrderStatus
    {
        Pending = 0,    // Yeni düştü
        Processing = 1, // Hazırlanıyor
        Shipped = 2,    // Kargolandı
        Completed = 3,  // Teslim Edildi
        Cancelled = 9   // İptal
    }
}
