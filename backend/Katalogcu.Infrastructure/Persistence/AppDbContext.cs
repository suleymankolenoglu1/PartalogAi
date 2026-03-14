using Katalogcu.Domain.Entities;
using Katalogcu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Mevcut Tablolar
        public DbSet<AppUser> Users { get; set; }
        public DbSet<Catalog> Catalogs { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<CatalogPage> CatalogPages { get; set; }
        public DbSet<Hotspot> Hotspots { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderStatusHistory> OrderStatusHistory { get; set; }
        public DbSet<CatalogItem> CatalogItems { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<ErpInventorySnapshot> ErpInventorySnapshots { get; set; }
        public DbSet<EmbedTarget> EmbedTargets { get; set; }
        public DbSet<CatalogAiJob> CatalogAiJobs { get; set; }
        public DbSet<PlatformAuditLog> PlatformAuditLogs { get; set; }

        // İlişki ve Davranış Ayarları
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 🔥 KRİTİK ADIM: PostgreSQL Vektör Eklentisini Aktif Et
            // Bu satır, veritabanına "vector" tipini tanıtır.
            modelBuilder.HasPostgresExtension("vector");

            base.OnModelCreating(modelBuilder);

            // 1. Sipariş (Order) ile Kalemleri (OrderItems) arasındaki ilişki
            modelBuilder.Entity<Order>()
                .HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade); 
                // ÖNEMLİ: Sipariş silinirse içindeki kalemler de silinsin.

            modelBuilder.Entity<Order>()
                .HasMany(o => o.StatusHistory)
                .WithOne()
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Sipariş Kalemi ile Ürün arasındaki ilişki
            modelBuilder.Entity<OrderItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict); 
                // GÜVENLİK: Eğer bir ürün satılmışsa, Products tablosundan silinemesin.
                
            // Fiyat alanları için hassasiyet ayarı
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.IdempotencyKey)
                .HasMaxLength(128);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OwnerUserId);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasIndex(h => new { h.OrderId, h.CreatedDate });

            modelBuilder.Entity<OrderStatusHistory>()
                .Property(h => h.Source)
                .HasMaxLength(64);

            modelBuilder.Entity<OrderStatusHistory>()
                .Property(h => h.IsVisibleToCustomer)
                .HasDefaultValue(true);

            modelBuilder.Entity<OrderStatusHistory>()
                .Property(h => h.Note)
                .HasMaxLength(512);

            modelBuilder.Entity<OrderStatusHistory>()
                .Property(h => h.ChangedBy)
                .HasMaxLength(256);

            modelBuilder.Entity<OrderItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<AppUser>()
                .Property(u => u.SubscriptionPlan)
                .HasConversion<int>()
                .HasDefaultValue(SubscriptionPlan.CatalogOnly);

            modelBuilder.Entity<AppUser>()
                .Property(u => u.MaxCatalogCount)
                .HasDefaultValue(3);

            modelBuilder.Entity<AppUser>()
                .Property(u => u.MaxPagePerCatalog)
                .HasDefaultValue(100);

            modelBuilder.Entity<AppUser>()
                .Property(u => u.PublicStoreSlug)
                .HasMaxLength(96);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.PublicStoreSlug)
                .IsUnique()
                .HasFilter("\"PublicStoreSlug\" IS NOT NULL");

            modelBuilder.Entity<CatalogPage>()
                .Property(p => p.ReviewStatus)
                .HasMaxLength(32)
                .HasDefaultValue("NeedsReview");

            modelBuilder.Entity<CatalogPage>()
                .Property(p => p.ReviewNotes)
                .HasMaxLength(1024);

            modelBuilder.Entity<Customer>()
                .Property(c => c.TotalSpent)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Customer>()
                .HasIndex(c => new { c.UserId, c.NormalizedPhone });

            modelBuilder.Entity<Customer>()
                .HasIndex(c => new { c.UserId, c.Email });

            modelBuilder.Entity<Customer>()
                .HasIndex(c => new { c.UserId, c.PublicSessionToken });

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.CustomerId);

            modelBuilder.Entity<StockMovement>()
                .HasIndex(m => new { m.UserId, m.CreatedDate });

            modelBuilder.Entity<StockMovement>()
                .HasIndex(m => new { m.ProductId, m.CreatedDate });

            modelBuilder.Entity<StockMovement>()
                .Property(m => m.MovementType)
                .HasMaxLength(32);

            modelBuilder.Entity<StockMovement>()
                .Property(m => m.ProductCode)
                .HasMaxLength(128);

            modelBuilder.Entity<StockMovement>()
                .Property(m => m.ProductName)
                .HasMaxLength(512);

            modelBuilder.Entity<StockMovement>()
                .Property(m => m.Reason)
                .HasMaxLength(1024);

            modelBuilder.Entity<StockMovement>()
                .Property(m => m.Source)
                .HasMaxLength(128);

            modelBuilder.Entity<StockMovement>()
                .Property(m => m.ActorName)
                .HasMaxLength(256);

            modelBuilder.Entity<StockMovement>()
                .Property(m => m.ReferenceId)
                .HasMaxLength(128);

            modelBuilder.Entity<StockMovement>()
                .HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ErpInventorySnapshot>()
                .HasIndex(x => new { x.OwnerUserId, x.Provider, x.PartCode });

            modelBuilder.Entity<ErpInventorySnapshot>()
                .HasIndex(x => new { x.OwnerUserId, x.Provider, x.ProductId });

            modelBuilder.Entity<ErpInventorySnapshot>()
                .HasIndex(x => new { x.OwnerUserId, x.Provider, x.ExternalProductId })
                .HasFilter("\"ExternalProductId\" IS NOT NULL");

            modelBuilder.Entity<ErpInventorySnapshot>()
                .Property(x => x.Provider)
                .HasMaxLength(64);

            modelBuilder.Entity<ErpInventorySnapshot>()
                .Property(x => x.ExternalProductId)
                .HasMaxLength(128);

            modelBuilder.Entity<ErpInventorySnapshot>()
                .Property(x => x.PartCode)
                .HasMaxLength(128);

            modelBuilder.Entity<ErpInventorySnapshot>()
                .Property(x => x.ProductName)
                .HasMaxLength(512);

            modelBuilder.Entity<ErpInventorySnapshot>()
                .Property(x => x.Currency)
                .HasMaxLength(8);

            modelBuilder.Entity<ErpInventorySnapshot>()
                .Property(x => x.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ErpInventorySnapshot>()
                .HasOne(x => x.Product)
                .WithMany(p => p.ErpInventorySnapshots)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<EmbedTarget>()
                .HasIndex(x => x.EmbedKey)
                .IsUnique();

            modelBuilder.Entity<EmbedTarget>()
                .HasIndex(x => new { x.UserId, x.Type, x.IsActive });

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.Name)
                .HasMaxLength(160);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.Type)
                .HasMaxLength(32);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.EmbedKey)
                .HasMaxLength(96);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.CommerceMode)
                .HasMaxLength(32);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.HostActionMode)
                .HasMaxLength(32);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.ProductUrlTemplate)
                .HasMaxLength(1024);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.SearchUrlTemplate)
                .HasMaxLength(1024);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.ExistingCartUrl)
                .HasMaxLength(1024);

            modelBuilder.Entity<EmbedTarget>()
                .Property(x => x.ExistingCartMethod)
                .HasMaxLength(16);

            modelBuilder.Entity<EmbedTarget>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmbedTarget>()
                .HasOne(x => x.Catalog)
                .WithMany()
                .HasForeignKey(x => x.CatalogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmbedTarget>()
                .HasOne(x => x.CatalogPage)
                .WithMany()
                .HasForeignKey(x => x.CatalogPageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CatalogAiJob>()
                .HasIndex(j => j.CatalogId)
                .IsUnique();

            modelBuilder.Entity<CatalogAiJob>()
                .HasIndex(j => new { j.Status, j.NextAttemptAt });

            modelBuilder.Entity<CatalogAiJob>()
                .Property(j => j.Status)
                .HasMaxLength(32);

            modelBuilder.Entity<CatalogAiJob>()
                .Property(j => j.LastError)
                .HasMaxLength(2048);

            modelBuilder.Entity<CatalogAiJob>()
                .HasOne(j => j.Catalog)
                .WithMany()
                .HasForeignKey(j => j.CatalogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlatformAuditLog>()
                .Property(x => x.Action)
                .HasMaxLength(128);

            modelBuilder.Entity<PlatformAuditLog>()
                .Property(x => x.ActorEmail)
                .HasMaxLength(256);

            modelBuilder.Entity<PlatformAuditLog>()
                .Property(x => x.ActorRole)
                .HasMaxLength(64);

            modelBuilder.Entity<PlatformAuditLog>()
                .Property(x => x.IpAddress)
                .HasMaxLength(64);

            modelBuilder.Entity<PlatformAuditLog>()
                .Property(x => x.UserAgent)
                .HasMaxLength(512);

            modelBuilder.Entity<PlatformAuditLog>()
                .HasIndex(x => x.TargetOwnerUserId);

            modelBuilder.Entity<PlatformAuditLog>()
                .HasIndex(x => x.ActorUserId);

            modelBuilder.Entity<PlatformAuditLog>()
                .HasIndex(x => x.CreatedDate);
        }
    }
}
