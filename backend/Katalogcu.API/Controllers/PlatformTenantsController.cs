using Katalogcu.Domain.Enums;
using Katalogcu.API.Services;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Katalogcu.API.Controllers;

[Authorize(Policy = "PlatformAdminOnly")]
[Route("api/platform/tenants")]
[ApiController]
public sealed class PlatformTenantsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IProductFeaturePolicy _featurePolicy;
    private readonly ILogger<PlatformTenantsController> _logger;

    public PlatformTenantsController(
        AppDbContext dbContext,
        IProductFeaturePolicy featurePolicy,
        ILogger<PlatformTenantsController> logger)
    {
        _dbContext = dbContext;
        _featurePolicy = featurePolicy;
        _logger = logger;
    }

    public sealed class UpdateTenantPlanRequest
    {
        public int Plan { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Reason { get; set; }
        public string? OperationId { get; set; }
    }

    public sealed class SuspendTenantRequest
    {
        public string? Reason { get; set; }
        public string? OperationId { get; set; }
    }

    public sealed class UnsuspendTenantRequest
    {
        public string? Reason { get; set; }
        public string? OperationId { get; set; }
    }

    private sealed class AuditEventDto
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public IReadOnlyList<AuditChangeDto>? Changes { get; set; }
        public string? OperationId { get; set; }
        public int? OperationCount { get; set; }
    }

    private sealed class AuditChangeDto
    {
        public string Field { get; init; } = string.Empty;
        public string? Before { get; init; }
        public string? After { get; init; }
    }

    [HttpGet]
    public async Task<IActionResult> GetTenants(
        [FromQuery] string? q = null,
        [FromQuery] int? plan = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var ownersQuery = _dbContext.Users
            .AsNoTracking()
            .Where(u => EF.Functions.ILike(u.Role, "owner") || EF.Functions.ILike(u.Role, "suspendedowner"));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            ownersQuery = ownersQuery.Where(u =>
                EF.Functions.ILike((u.FirstName + " " + u.LastName).Trim(), $"%{term}%") ||
                EF.Functions.ILike(u.Email, $"%{term}%") ||
                (u.CompanyName != null && EF.Functions.ILike(u.CompanyName, $"%{term}%")));
        }

        if (plan is >= 1 and <= 3)
        {
            ownersQuery = ownersQuery.Where(u => (int)u.SubscriptionPlan == plan.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (normalizedStatus == "active")
            {
                ownersQuery = ownersQuery.Where(u => EF.Functions.ILike(u.Role, "owner"));
            }
            else if (normalizedStatus == "suspended")
            {
                ownersQuery = ownersQuery.Where(u => EF.Functions.ILike(u.Role, "suspendedowner"));
            }
        }

        var owners = await ownersQuery
            .OrderByDescending(u => u.CreatedDate)
            .Select(u => new
            {
                u.Id,
                FullName = (u.FirstName + " " + u.LastName).Trim(),
                u.Email,
                u.Role,
                u.CompanyName,
                u.PhoneNumber,
                u.SubscriptionPlan,
                u.PlanExpiresAt,
                u.MaxCatalogCount,
                u.MaxPagePerCatalog,
                u.CreatedDate,
                u.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        var ownerIds = owners.Select(x => x.Id).ToArray();
        var catalogCounts = await _dbContext.Catalogs
            .AsNoTracking()
            .Where(c => ownerIds.Contains(c.UserId))
            .GroupBy(c => c.UserId)
            .Select(g => new
            {
                OwnerId = g.Key,
                CatalogCount = g.Count(),
                LastCatalogAt = g.Max(c => c.UpdatedDate ?? c.CreatedDate)
            })
            .ToDictionaryAsync(x => x.OwnerId, x => new { x.CatalogCount, x.LastCatalogAt }, cancellationToken);

        var partCounts = await (
                from item in _dbContext.CatalogItems.AsNoTracking()
                join catalog in _dbContext.Catalogs.AsNoTracking() on item.CatalogId equals catalog.Id
                where ownerIds.Contains(catalog.UserId)
                group item by catalog.UserId
                into g
                select new
                {
                    OwnerId = g.Key,
                    PartCount = g.Count()
                })
            .ToDictionaryAsync(x => x.OwnerId, x => x.PartCount, cancellationToken);

        var customerCounts = new Dictionary<Guid, int>();
        var orderCounts = new Dictionary<Guid, int>();
        if (_featurePolicy.EcommerceEnabled)
        {
            customerCounts = await _dbContext.Customers
                .AsNoTracking()
                .Where(c => ownerIds.Contains(c.UserId))
                .GroupBy(c => c.UserId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.OwnerId, x => x.Count, cancellationToken);

            orderCounts = await _dbContext.Orders
                .AsNoTracking()
                .Where(o => o.OwnerUserId.HasValue && ownerIds.Contains(o.OwnerUserId.Value))
                .GroupBy(o => o.OwnerUserId!.Value)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.OwnerId, x => x.Count, cancellationToken);
        }

        var tenants = owners.Select(owner =>
        {
            var catalogStat = catalogCounts.GetValueOrDefault(owner.Id);
            var partCount = partCounts.GetValueOrDefault(owner.Id);
            var plan = (SubscriptionPlan)owner.SubscriptionPlan;

            return new
            {
                ownerId = owner.Id,
                ownerFullName = owner.FullName,
                ownerEmail = owner.Email,
                companyName = owner.CompanyName,
                phoneNumber = owner.PhoneNumber,
                isSuspended = string.Equals(owner.Role, "SuspendedOwner", StringComparison.OrdinalIgnoreCase),
                plan = (int)plan,
                planName = plan.ToString(),
                planExpiresAt = owner.PlanExpiresAt,
                limits = new
                {
                    maxCatalogCount = owner.MaxCatalogCount,
                    maxPagePerCatalog = owner.MaxPagePerCatalog
                },
                usage = new
                {
                    catalogCount = catalogStat?.CatalogCount ?? 0,
                    partCount,
                    customerCount = customerCounts.GetValueOrDefault(owner.Id),
                    orderCount = orderCounts.GetValueOrDefault(owner.Id),
                    lastCatalogAt = catalogStat?.LastCatalogAt
                },
                createdAt = owner.CreatedDate,
                updatedAt = owner.UpdatedDate
            };
        });

        return Ok(new
        {
            total = tenants.Count(),
            items = tenants
        });
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken)
    {
        var tenantsBaseQuery = _dbContext.Users
            .AsNoTracking()
            .Where(u => EF.Functions.ILike(u.Role, "owner") || EF.Functions.ILike(u.Role, "suspendedowner"));

        var tenantRows = await tenantsBaseQuery
            .Select(u => new
            {
                u.Id,
                u.Role,
                u.SubscriptionPlan
            })
            .ToListAsync(cancellationToken);

        var ownerIds = tenantRows.Select(x => x.Id).ToArray();

        var catalogCount = await _dbContext.Catalogs
            .AsNoTracking()
            .CountAsync(c => ownerIds.Contains(c.UserId), cancellationToken);

        var partCount = await (
                from item in _dbContext.CatalogItems.AsNoTracking()
                join catalog in _dbContext.Catalogs.AsNoTracking() on item.CatalogId equals catalog.Id
                where ownerIds.Contains(catalog.UserId)
                select item.Id)
            .CountAsync(cancellationToken);

        var orderCount = 0;
        if (_featurePolicy.EcommerceEnabled)
        {
            orderCount = await _dbContext.Orders
                .AsNoTracking()
                .CountAsync(o => o.OwnerUserId.HasValue && ownerIds.Contains(o.OwnerUserId.Value), cancellationToken);
        }

        var aiJobCount = 0;
        if (_featurePolicy.AiEnabled)
        {
            aiJobCount = await (
                    from job in _dbContext.CatalogAiJobs.AsNoTracking()
                    join catalog in _dbContext.Catalogs.AsNoTracking() on job.CatalogId equals catalog.Id
                    where ownerIds.Contains(catalog.UserId)
                    select job.Id)
                .CountAsync(cancellationToken);
        }

        var activeCount = tenantRows.Count(t => string.Equals(t.Role, "Owner", StringComparison.OrdinalIgnoreCase));
        var suspendedCount = tenantRows.Count(t => string.Equals(t.Role, "SuspendedOwner", StringComparison.OrdinalIgnoreCase));

        var planDistribution = tenantRows
            .GroupBy(t => t.SubscriptionPlan)
            .Select(g => new
            {
                plan = (int)g.Key,
                planName = g.Key.ToString(),
                count = g.Count()
            })
            .OrderBy(x => x.plan)
            .ToList();

        return Ok(new
        {
            totals = new
            {
                tenants = tenantRows.Count,
                activeTenants = activeCount,
                suspendedTenants = suspendedCount,
                catalogs = catalogCount,
                parts = partCount,
                orders = orderCount,
                aiJobs = aiJobCount
            },
            plans = planDistribution
        });
    }

    [HttpGet("{ownerId:guid}")]
    public async Task<IActionResult> GetTenantDetail(Guid ownerId, [FromQuery] int months = 6, CancellationToken cancellationToken = default)
    {
        var monthCount = Math.Clamp(months, 3, 12);
        var owner = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerId && (EF.Functions.ILike(u.Role, "owner") || EF.Functions.ILike(u.Role, "suspendedowner")))
            .Select(u => new
            {
                u.Id,
                FullName = (u.FirstName + " " + u.LastName).Trim(),
                u.Email,
                u.CompanyName,
                u.PhoneNumber,
                u.Role,
                u.SubscriptionPlan,
                u.MaxCatalogCount,
                u.MaxPagePerCatalog,
                u.PublicLinkEnabled,
                u.CreatedDate,
                u.UpdatedDate,
                u.PlanActivatedAt,
                u.PlanExpiresAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (owner == null)
        {
            return NotFound(new { message = "İşletme sahibi bulunamadı." });
        }

        var catalogTotal = await _dbContext.Catalogs
            .AsNoTracking()
            .Where(c => c.UserId == ownerId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                CatalogCount = g.Count(),
                LastCatalogAt = g.Max(x => x.UpdatedDate ?? x.CreatedDate)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var partCount = await (
                from item in _dbContext.CatalogItems.AsNoTracking()
                join catalog in _dbContext.Catalogs.AsNoTracking() on item.CatalogId equals catalog.Id
                where catalog.UserId == ownerId
                group item by 1
                into g
                select g.Count())
            .FirstOrDefaultAsync(cancellationToken);

        var recentCatalogs = await _dbContext.Catalogs
            .AsNoTracking()
            .Where(c => c.UserId == ownerId)
            .OrderByDescending(c => c.UpdatedDate ?? c.CreatedDate)
            .Take(10)
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                status = c.Status,
                createdAt = c.CreatedDate,
                updatedAt = c.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<object> monthlyUsage = Array.Empty<object>();
        try
        {
            monthlyUsage = await BuildMonthlyUsage(ownerId, monthCount, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform tenant detail monthly usage failed for owner {OwnerId}", ownerId);
        }

        IReadOnlyList<object> audit = Array.Empty<object>();
        try
        {
            audit = await BuildTenantAuditLog(
                ownerId,
                owner.CreatedDate,
                owner.UpdatedDate,
                owner.Email,
                owner.Role,
                owner.PlanActivatedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform tenant detail audit build failed for owner {OwnerId}", ownerId);
        }

        var plan = (SubscriptionPlan)owner.SubscriptionPlan;
        var orderCount = 0;
        DateTime? lastOrderAt = null;
        var customerCount = 0;
        var activeCustomerCount = 0;
        decimal totalCustomerRevenue = 0;
        DateTime? lastCustomerOrderAt = null;
        var topCustomers = new List<object>();
        var recentOrders = new List<object>();
        if (_featurePolicy.EcommerceEnabled)
        {
            try
            {
                var orderTotal = await _dbContext.Orders
                    .AsNoTracking()
                    .Where(o => o.OwnerUserId == ownerId)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        OrderCount = g.Count(),
                        LastOrderAt = g.Max(o => o.CreatedDate)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                orderCount = orderTotal?.OrderCount ?? 0;
                lastOrderAt = orderTotal?.LastOrderAt;

                var customerTotals = await _dbContext.Customers
                    .AsNoTracking()
                    .Where(c => c.UserId == ownerId)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        CustomerCount = g.Count(),
                        ActiveCustomerCount = g.Count(x => x.IsActive),
                        TotalRevenue = g.Sum(x => x.TotalSpent),
                        LastCustomerOrderAt = g.Max(x => x.LastOrderDate)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                customerCount = customerTotals?.CustomerCount ?? 0;
                activeCustomerCount = customerTotals?.ActiveCustomerCount ?? 0;
                totalCustomerRevenue = customerTotals?.TotalRevenue ?? 0;
                lastCustomerOrderAt = customerTotals?.LastCustomerOrderAt;

                var topCustomersRaw = await _dbContext.Customers
                    .AsNoTracking()
                    .Where(c => c.UserId == ownerId)
                    .OrderByDescending(c => c.LastOrderDate ?? c.UpdatedDate ?? c.CreatedDate)
                    .ThenByDescending(c => c.OrderCount)
                    .Take(10)
                    .Select(c => new
                    {
                        id = c.Id,
                        fullName = c.FullName,
                        phone = c.Phone,
                        email = c.Email,
                        isActive = c.IsActive,
                        orderCount = c.OrderCount,
                        totalSpent = c.TotalSpent,
                        lastOrderDate = c.LastOrderDate,
                        lastLoginDate = c.LastLoginDate
                    })
                    .ToListAsync(cancellationToken);

                topCustomers = topCustomersRaw
                    .Select(c => (object)c)
                    .ToList();

                var ordersRaw = await _dbContext.Orders
                    .AsNoTracking()
                    .Where(o => o.OwnerUserId == ownerId)
                    .OrderByDescending(o => o.CreatedDate)
                    .Take(20)
                    .Select(o => new
                    {
                        o.Id,
                        o.OrderNumber,
                        StatusValue = (int)o.Status,
                        o.TotalAmount,
                        o.CreatedDate,
                        o.CustomerName,
                        o.CustomerPhone,
                        o.CustomerEmail,
                        o.DeliveryAddress,
                        o.DeliveryCity,
                        o.DeliveryDistrict,
                        o.DeliveryNote,
                        o.PaymentMethod,
                        ItemCount = o.Items.Count
                    })
                    .ToListAsync(cancellationToken);

                var orderIds = ordersRaw.Select(x => x.Id).ToArray();
                var itemsRaw = await (
                        from item in _dbContext.OrderItems.AsNoTracking()
                        join product in _dbContext.Products.AsNoTracking() on item.ProductId equals product.Id
                        where orderIds.Contains(item.OrderId)
                        select new
                        {
                            item.OrderId,
                            item.ProductId,
                            item.Quantity,
                            item.UnitPrice,
                            product.Name,
                            product.Code
                        })
                    .ToListAsync(cancellationToken);

                var itemMap = itemsRaw
                    .GroupBy(x => x.OrderId)
                    .ToDictionary(
                        g => g.Key,
                        g => (object)g
                            .Select(x => new
                            {
                                productId = x.ProductId,
                                productCode = x.Code,
                                productName = x.Name,
                                quantity = x.Quantity,
                                unitPrice = x.UnitPrice,
                                lineTotal = x.UnitPrice * x.Quantity
                            })
                            .Take(6)
                            .ToList());

                recentOrders = ordersRaw
                    .Select(o => (object)new
                    {
                        id = o.Id,
                        orderNumber = o.OrderNumber,
                        status = ((OrderStatus)o.StatusValue).ToString(),
                        totalAmount = o.TotalAmount,
                        createdAt = o.CreatedDate,
                        customerName = o.CustomerName,
                        customerPhone = o.CustomerPhone,
                        customerEmail = o.CustomerEmail,
                        deliveryAddress = o.DeliveryAddress,
                        deliveryCity = o.DeliveryCity,
                        deliveryDistrict = o.DeliveryDistrict,
                        deliveryNote = o.DeliveryNote,
                        paymentMethod = o.PaymentMethod,
                        itemCount = o.ItemCount,
                        items = itemMap.GetValueOrDefault(o.Id, new List<object>())
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Platform tenant detail ecommerce section failed for owner {OwnerId}", ownerId);
            }
        }

        var aiJobCount = 0;
        if (_featurePolicy.AiEnabled)
        {
            aiJobCount = await (
                    from job in _dbContext.CatalogAiJobs.AsNoTracking()
                    join catalog in _dbContext.Catalogs.AsNoTracking() on job.CatalogId equals catalog.Id
                    where catalog.UserId == ownerId
                    group job by 1
                    into g
                    select g.Count())
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Ok(new
        {
            ownerId = owner.Id,
            ownerFullName = owner.FullName,
            ownerEmail = owner.Email,
            companyName = owner.CompanyName,
            phoneNumber = owner.PhoneNumber,
            role = owner.Role,
            isSuspended = string.Equals(owner.Role, "SuspendedOwner", StringComparison.OrdinalIgnoreCase),
            publicLinkEnabled = owner.PublicLinkEnabled,
            plan = (int)plan,
            planName = plan.ToString(),
            planActivatedAt = owner.PlanActivatedAt,
            planExpiresAt = owner.PlanExpiresAt,
            limits = new
            {
                maxCatalogCount = owner.MaxCatalogCount,
                maxPagePerCatalog = owner.MaxPagePerCatalog
            },
            usageTotals = new
            {
                catalogCount = catalogTotal?.CatalogCount ?? 0,
                partCount,
                orderCount,
                aiJobCount,
                lastCatalogAt = catalogTotal?.LastCatalogAt,
                lastOrderAt
            },
            ecommerceEnabled = _featurePolicy.EcommerceEnabled,
            customerTotals = new
            {
                customerCount,
                activeCustomerCount,
                totalRevenue = totalCustomerRevenue,
                lastOrderAt = lastCustomerOrderAt
            },
            topCustomers,
            recentOrders,
            monthlyUsage,
            recentCatalogs,
            auditLog = audit
        });
    }

    [HttpGet("{ownerId:guid}/audit")]
    public async Task<IActionResult> GetTenantAudit(Guid ownerId, CancellationToken cancellationToken)
    {
        var owner = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerId && (EF.Functions.ILike(u.Role, "owner") || EF.Functions.ILike(u.Role, "suspendedowner")))
            .Select(u => new
            {
                u.Id,
                FullName = (u.FirstName + " " + u.LastName).Trim(),
                u.Email,
                u.Role,
                u.CreatedDate,
                u.UpdatedDate,
                u.PlanActivatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (owner == null)
        {
            return NotFound(new { message = "İşletme sahibi bulunamadı." });
        }

        var events = await BuildTenantAuditLog(
            ownerId,
            owner.CreatedDate,
            owner.UpdatedDate,
            owner.Email,
            owner.Role,
            owner.PlanActivatedAt,
            cancellationToken);
        return Ok(new { ownerId, total = events.Count, items = events });
    }

    [HttpPatch("{ownerId:guid}/plan")]
    public async Task<IActionResult> UpdatePlan(Guid ownerId, [FromBody] UpdateTenantPlanRequest request, CancellationToken cancellationToken)
    {
        var owner = await _dbContext.Users
            .FirstOrDefaultAsync(
                u => u.Id == ownerId && (EF.Functions.ILike(u.Role, "owner") || EF.Functions.ILike(u.Role, "suspendedowner")),
                cancellationToken);

        if (owner == null)
        {
            return NotFound(new { message = "İşletme sahibi bulunamadı." });
        }

        if (!Enum.IsDefined(typeof(SubscriptionPlan), request.Plan))
        {
            return BadRequest(new { message = "Geçersiz plan değeri." });
        }

        var plan = (SubscriptionPlan)request.Plan;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 300 })
        {
            return BadRequest(new { message = "İşlem notu en fazla 300 karakter olabilir." });
        }
        var operationId = NormalizeOperationId(request.OperationId);

        var limits = PlanLimitRules.For(plan);
        var beforePlan = owner.SubscriptionPlan;
        var beforeMaxCatalogCount = owner.MaxCatalogCount;
        var beforeExpiresAt = owner.PlanExpiresAt;

        owner.SubscriptionPlan = plan;
        owner.PlanActivatedAt ??= DateTime.UtcNow;
        owner.PlanExpiresAt = request.ExpiresAt;
        owner.MaxCatalogCount = limits.MaxCatalogCount ?? int.MaxValue;
        owner.UpdatedDate = DateTime.UtcNow;
        AddAuditLogEntry(
            action: "TENANT_PLAN_UPDATED",
            targetOwnerId: owner.Id,
            payload: new
            {
                operationId,
                reason,
                before = new
                {
                    plan = (int)beforePlan,
                    planName = beforePlan.ToString(),
                    maxCatalogCount = beforeMaxCatalogCount,
                    expiresAt = beforeExpiresAt
                },
                after = new
                {
                    plan = (int)plan,
                    planName = plan.ToString(),
                    maxCatalogCount = owner.MaxCatalogCount,
                    expiresAt = owner.PlanExpiresAt
                }
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Plan güncellendi.",
            ownerId = owner.Id,
            plan = (int)owner.SubscriptionPlan,
            planName = owner.SubscriptionPlan.ToString(),
            maxCatalogCount = owner.MaxCatalogCount,
            expiresAt = owner.PlanExpiresAt,
            reason,
            operationId
        });
    }

    [HttpPost("{ownerId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid ownerId, [FromBody] SuspendTenantRequest? request, CancellationToken cancellationToken)
    {
        var suspendReasonRaw = request?.Reason;
        var suspendReason = string.IsNullOrWhiteSpace(suspendReasonRaw) ? null : suspendReasonRaw.Trim();
        if (suspendReason is { Length: > 300 })
        {
            return BadRequest(new { message = "İşlem notu en fazla 300 karakter olabilir." });
        }
        var operationId = NormalizeOperationId(request?.OperationId);

        var owner = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == ownerId && EF.Functions.ILike(u.Role, "owner"), cancellationToken);

        if (owner == null)
        {
            return NotFound(new { message = "İşletme sahibi bulunamadı." });
        }

        var beforeRole = owner.Role;
        var beforePublicLinkEnabled = owner.PublicLinkEnabled;
        owner.Role = "SuspendedOwner";
        owner.PublicLinkEnabled = false;
        owner.UpdatedDate = DateTime.UtcNow;
        AddAuditLogEntry(
            action: "TENANT_SUSPENDED",
            targetOwnerId: owner.Id,
            payload: new
            {
                operationId,
                reason = suspendReason,
                before = new
                {
                    role = beforeRole,
                    publicLinkEnabled = beforePublicLinkEnabled
                },
                after = new
                {
                    role = owner.Role,
                    publicLinkEnabled = owner.PublicLinkEnabled
                }
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "İşletme askıya alındı.",
            ownerId = owner.Id,
            suspended = true,
            reason = suspendReason,
            operationId
        });
    }

    [HttpPost("{ownerId:guid}/unsuspend")]
    public async Task<IActionResult> Unsuspend(Guid ownerId, [FromBody] UnsuspendTenantRequest? request, CancellationToken cancellationToken)
    {
        var unsuspendReasonRaw = request?.Reason;
        var unsuspendReason = string.IsNullOrWhiteSpace(unsuspendReasonRaw) ? null : unsuspendReasonRaw.Trim();
        if (unsuspendReason is { Length: > 300 })
        {
            return BadRequest(new { message = "İşlem notu en fazla 300 karakter olabilir." });
        }
        var operationId = NormalizeOperationId(request?.OperationId);

        var owner = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == ownerId && EF.Functions.ILike(u.Role, "SuspendedOwner"), cancellationToken);

        if (owner == null)
        {
            return NotFound(new { message = "Askıya alınmış işletme bulunamadı." });
        }

        var beforeRole = owner.Role;
        var beforePublicLinkEnabled = owner.PublicLinkEnabled;
        owner.Role = "Owner";
        owner.PublicLinkEnabled = true;
        owner.UpdatedDate = DateTime.UtcNow;
        AddAuditLogEntry(
            action: "TENANT_UNSUSPENDED",
            targetOwnerId: owner.Id,
            payload: new
            {
                operationId,
                reason = unsuspendReason,
                before = new
                {
                    role = beforeRole,
                    publicLinkEnabled = beforePublicLinkEnabled
                },
                after = new
                {
                    role = owner.Role,
                    publicLinkEnabled = owner.PublicLinkEnabled
                }
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "İşletme yeniden aktifleştirildi.",
            ownerId = owner.Id,
            suspended = false,
            reason = unsuspendReason,
            operationId
        });
    }

    private async Task<IReadOnlyList<object>> BuildMonthlyUsage(Guid ownerId, int monthCount, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStarts = Enumerable.Range(0, monthCount)
            .Select(i => new DateTime(now.Year, now.Month, 1).AddMonths(-(monthCount - 1 - i)))
            .ToArray();
        var rangeStart = monthStarts.First();

        var catalogByMonth = await _dbContext.Catalogs
            .AsNoTracking()
            .Where(c => c.UserId == ownerId && c.CreatedDate >= rangeStart)
            .GroupBy(c => new { c.CreatedDate.Year, c.CreatedDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var partByMonth = await (
                from item in _dbContext.CatalogItems.AsNoTracking()
                join catalog in _dbContext.Catalogs.AsNoTracking() on item.CatalogId equals catalog.Id
                where catalog.UserId == ownerId && item.CreatedDate >= rangeStart
                group item by new { item.CreatedDate.Year, item.CreatedDate.Month }
                into g
                select new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var catalogMap = catalogByMonth.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
        var partMap = partByMonth.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
        var orderMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var aiMap = new Dictionary<string, int>(StringComparer.Ordinal);

        if (_featurePolicy.EcommerceEnabled)
        {
            var orderByMonth = await _dbContext.Orders
                .AsNoTracking()
                .Where(o => o.OwnerUserId == ownerId && o.CreatedDate >= rangeStart)
                .GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync(cancellationToken);
            orderMap = orderByMonth.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
        }

        if (_featurePolicy.AiEnabled)
        {
            var aiByMonth = await (
                    from job in _dbContext.CatalogAiJobs.AsNoTracking()
                    join catalog in _dbContext.Catalogs.AsNoTracking() on job.CatalogId equals catalog.Id
                    where catalog.UserId == ownerId && job.CreatedDate >= rangeStart
                    group job by new { job.CreatedDate.Year, job.CreatedDate.Month }
                    into g
                    select new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync(cancellationToken);
            aiMap = aiByMonth.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
        }

        return monthStarts.Select(start =>
        {
            var key = $"{start.Year:D4}-{start.Month:D2}";
            return (object)new
            {
                month = key,
                catalogs = catalogMap.GetValueOrDefault(key),
                parts = partMap.GetValueOrDefault(key),
                orders = orderMap.GetValueOrDefault(key),
                aiJobs = aiMap.GetValueOrDefault(key)
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<object>> BuildTenantAuditLog(
        Guid ownerId,
        DateTime ownerCreatedAt,
        DateTime? ownerUpdatedAt,
        string ownerEmail,
        string ownerRole,
        DateTime? planActivatedAt,
        CancellationToken cancellationToken)
    {
        var events = new List<AuditEventDto>
        {
            new()
            {
                Timestamp = ownerCreatedAt,
                Type = "TenantCreated",
                Title = "İşletme hesabı oluşturuldu",
                Detail = ownerEmail
            }
        };

        if (planActivatedAt is DateTime activatedAt)
        {
            events.Add(new AuditEventDto
            {
                Timestamp = activatedAt,
                Type = "PlanActivated",
                Title = "Paket aktivasyonu",
                Detail = null
            });
        }

        if (string.Equals(ownerRole, "SuspendedOwner", StringComparison.OrdinalIgnoreCase) && ownerUpdatedAt is DateTime suspendedAt)
        {
            events.Add(new AuditEventDto
            {
                Timestamp = suspendedAt,
                Type = "Suspended",
                Title = "İşletme askıya alındı",
                Detail = null
            });
        }

        var platformLogsRaw = await _dbContext.PlatformAuditLogs
            .AsNoTracking()
            .Where(l => l.TargetOwnerUserId == ownerId)
            .OrderByDescending(l => l.CreatedDate)
            .Take(40)
            .Select(l => new
            {
                l.CreatedDate,
                l.Action,
                l.ActorEmail,
                l.ActorRole,
                l.IpAddress,
                l.Payload
            })
            .ToListAsync(cancellationToken);
        var platformLogs = platformLogsRaw.Select(l => new AuditEventDto
        {
            Timestamp = l.CreatedDate,
            Type = "PlatformAction",
            Title = MapPlatformActionTitle(l.Action),
            OperationId = ExtractOperationId(l.Payload),
            Detail = BuildPlatformAuditDetail(
                l.Action,
                l.ActorEmail,
                l.ActorRole,
                l.IpAddress,
                l.Payload),
            Changes = ExtractAuditChanges(l.Action, l.Payload)
        });
        events.AddRange(platformLogs);

        var collapsedEvents = CollapseOperationEvents(events);
        return collapsedEvents
            .OrderByDescending(x => x.Timestamp)
            .Take(40)
            .Select(x => (object)new
            {
                timestamp = x.Timestamp,
                type = x.Type,
                title = x.Title,
                detail = x.Detail,
                operationId = x.OperationId,
                operationCount = x.OperationCount,
                changes = x.Changes == null
                    ? null
                    : x.Changes.Select(c => new
                    {
                        field = c.Field,
                        before = c.Before,
                        after = c.After
                    })
            })
            .ToList();
    }

    private void AddAuditLogEntry(string action, Guid targetOwnerId, object? payload)
    {
        Guid? actorUserId = null;
        var actorIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(actorIdRaw, out var parsedActorId))
        {
            actorUserId = parsedActorId;
        }

        var actorEmail = User.FindFirstValue(ClaimTypes.Email);
        var actorRole = User.FindFirstValue(ClaimTypes.Role);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        _dbContext.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorUserId = actorUserId,
            TargetOwnerUserId = targetOwnerId,
            Action = action,
            ActorEmail = actorEmail,
            ActorRole = actorRole,
            IpAddress = ip,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload),
            CreatedDate = DateTime.UtcNow
        });
    }

    private static string MapPlatformActionTitle(string action)
    {
        return action switch
        {
            "TENANT_PLAN_UPDATED" => "Paket güncellendi",
            "TENANT_SUSPENDED" => "İşletme askıya alındı",
            "TENANT_UNSUSPENDED" => "İşletme aktifleştirildi",
            _ => action
        };
    }

    private static string? BuildPlatformAuditDetail(string action, string? actorEmail, string? actorRole, string? ipAddress, string? payload)
    {
        var actorText = string.IsNullOrWhiteSpace(actorEmail)
            ? (string.IsNullOrWhiteSpace(actorRole) ? "Bilinmeyen kullanıcı" : actorRole)
            : actorEmail;
        var ipText = string.IsNullOrWhiteSpace(ipAddress) ? string.Empty : $" | IP: {ipAddress}";

        if (string.IsNullOrWhiteSpace(payload))
        {
            return $"{actorText}{ipText}".Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            return action switch
            {
                "TENANT_PLAN_UPDATED" => BuildPlanUpdatedDetail(actorText, ipText, root),
                "TENANT_SUSPENDED" => BuildSuspendedDetail(actorText, ipText, root),
                "TENANT_UNSUSPENDED" => BuildUnsuspendedDetail(actorText, ipText, root),
                _ => $"{actorText}{ipText}"
            };
        }
        catch
        {
            var payloadText = payload.Length > 220 ? payload[..220] + "..." : payload;
            return $"{actorText}{ipText} | {payloadText}".Trim();
        }
    }

    private static string BuildPlanUpdatedDetail(string actorText, string ipText, JsonElement root)
    {
        var reason = TryGetNestedString(root, "reason");
        var beforePlan = TryGetNestedString(root, "before", "planName") ?? "-";
        var afterPlan = TryGetNestedString(root, "after", "planName") ?? "-";
        var beforeLimit = TryGetNestedInt(root, "before", "maxCatalogCount");
        var afterLimit = TryGetNestedInt(root, "after", "maxCatalogCount");
        var beforeExpires = TryGetNestedDate(root, "before", "expiresAt");
        var afterExpires = TryGetNestedDate(root, "after", "expiresAt");

        var limitText = $"{(beforeLimit?.ToString() ?? "-")} -> {(afterLimit?.ToString() ?? "-")}";
        var expiresText = $"{FormatDateOrDash(beforeExpires)} -> {FormatDateOrDash(afterExpires)}";
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "not yok" : reason;
        return $"{actorText} planı güncelledi: {beforePlan} -> {afterPlan} | Katalog limiti: {limitText} | Bitiş: {expiresText} | Not: {reasonText}{ipText}";
    }

    private static string BuildSuspendedDetail(string actorText, string ipText, JsonElement root)
    {
        var reason = TryGetNestedString(root, "reason");
        var beforeRole = TryGetNestedString(root, "before", "role") ?? "Owner";
        var afterRole = TryGetNestedString(root, "after", "role") ?? "SuspendedOwner";
        var beforePublic = TryGetNestedBool(root, "before", "publicLinkEnabled");
        var afterPublic = TryGetNestedBool(root, "after", "publicLinkEnabled");
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "yok" : reason;
        return $"{actorText} işletmeyi askıya aldı: rol {beforeRole} -> {afterRole}, public link {(beforePublic?.ToString() ?? "-")} -> {(afterPublic?.ToString() ?? "-")} | Sebep: {reasonText}{ipText}";
    }

    private static string BuildUnsuspendedDetail(string actorText, string ipText, JsonElement root)
    {
        var reason = TryGetNestedString(root, "reason");
        var beforeRole = TryGetNestedString(root, "before", "role") ?? "SuspendedOwner";
        var afterRole = TryGetNestedString(root, "after", "role") ?? "Owner";
        var beforePublic = TryGetNestedBool(root, "before", "publicLinkEnabled");
        var afterPublic = TryGetNestedBool(root, "after", "publicLinkEnabled");
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "yok" : reason;
        return $"{actorText} işletmeyi aktifleştirdi: rol {beforeRole} -> {afterRole}, public link {(beforePublic?.ToString() ?? "-")} -> {(afterPublic?.ToString() ?? "-")} | Sebep: {reasonText}{ipText}";
    }

    private static string? TryGetNestedString(JsonElement root, params string[] path)
    {
        if (!TryGetNested(root, out var value, path)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? TryGetNestedInt(JsonElement root, params string[] path)
    {
        if (!TryGetNested(root, out var value, path)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
        {
            return n;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out n))
        {
            return n;
        }

        return null;
    }

    private static bool? TryGetNestedBool(JsonElement root, params string[] path)
    {
        if (!TryGetNested(root, out var value, path)) return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var b))
        {
            return b;
        }

        return null;
    }

    private static DateTime? TryGetNestedDate(JsonElement root, params string[] path)
    {
        if (!TryGetNested(root, out var value, path)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var dt))
        {
            return dt.ToUniversalTime();
        }

        return null;
    }

    private static bool TryGetNested(JsonElement root, out JsonElement value, params string[] path)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out var next))
            {
                value = default;
                return false;
            }

            value = next;
        }

        return true;
    }

    private static string FormatDateOrDash(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "-";
    }

    private static IReadOnlyList<AuditEventDto> CollapseOperationEvents(IEnumerable<AuditEventDto> source)
    {
        var items = source.ToList();
        var grouped = items
            .Where(x =>
                x.Type == "PlatformAction" &&
                !string.IsNullOrWhiteSpace(x.OperationId))
            .GroupBy(x => x.OperationId!, StringComparer.Ordinal);

        var skipSet = new HashSet<AuditEventDto>();
        var merged = new List<AuditEventDto>();

        foreach (var group in grouped)
        {
            var groupItems = group
                .OrderByDescending(x => x.Timestamp)
                .ToList();

            if (groupItems.Count <= 1)
            {
                continue;
            }

            var first = groupItems[0];
            foreach (var item in groupItems)
            {
                skipSet.Add(item);
            }

            var mergedChanges = groupItems
                .SelectMany(x => x.Changes ?? [])
                .ToList();

            merged.Add(new AuditEventDto
            {
                Timestamp = first.Timestamp,
                Type = first.Type,
                Title = $"{first.Title} ({groupItems.Count} kayıt)",
                Detail = first.Detail,
                OperationId = first.OperationId,
                OperationCount = groupItems.Count,
                Changes = mergedChanges.Count == 0 ? null : mergedChanges
            });
        }

        foreach (var item in items)
        {
            if (!skipSet.Contains(item))
            {
                merged.Add(item);
            }
        }

        return merged;
    }

    private static string? ExtractOperationId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return TryGetNestedString(doc.RootElement, "operationId");
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<AuditChangeDto>? ExtractAuditChanges(string action, string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var changes = action switch
            {
                "TENANT_PLAN_UPDATED" => ExtractPlanChanges(root),
                "TENANT_SUSPENDED" => ExtractSuspendChanges(root),
                "TENANT_UNSUSPENDED" => ExtractUnsuspendChanges(root),
                _ => []
            };

            return changes.Count == 0 ? null : changes;
        }
        catch
        {
            return null;
        }
    }

    private static List<AuditChangeDto> ExtractPlanChanges(JsonElement root)
    {
        var changes = new List<AuditChangeDto>();
        AddChangeIfDifferent(
            changes,
            field: "Paket",
            before: TryGetNestedString(root, "before", "planName"),
            after: TryGetNestedString(root, "after", "planName"));
        AddChangeIfDifferent(
            changes,
            field: "Katalog Limiti",
            before: ToStringOrNull(TryGetNestedInt(root, "before", "maxCatalogCount")),
            after: ToStringOrNull(TryGetNestedInt(root, "after", "maxCatalogCount")));
        AddChangeIfDifferent(
            changes,
            field: "Bitiş Tarihi",
            before: FormatDateOrDash(TryGetNestedDate(root, "before", "expiresAt")),
            after: FormatDateOrDash(TryGetNestedDate(root, "after", "expiresAt")));
        AddChangeIfDifferent(
            changes,
            field: "İşlem Notu",
            before: "-",
            after: TryGetNestedString(root, "reason"));

        return changes;
    }

    private static List<AuditChangeDto> ExtractSuspendChanges(JsonElement root)
    {
        var changes = new List<AuditChangeDto>();
        AddChangeIfDifferent(
            changes,
            field: "Rol",
            before: TryGetNestedString(root, "before", "role"),
            after: TryGetNestedString(root, "after", "role"));
        AddChangeIfDifferent(
            changes,
            field: "Public Link",
            before: ToStringOrNull(TryGetNestedBool(root, "before", "publicLinkEnabled")),
            after: ToStringOrNull(TryGetNestedBool(root, "after", "publicLinkEnabled")));

        return changes;
    }

    private static List<AuditChangeDto> ExtractUnsuspendChanges(JsonElement root)
    {
        var changes = ExtractSuspendChanges(root);
        AddChangeIfDifferent(
            changes,
            field: "İşlem Notu",
            before: "-",
            after: TryGetNestedString(root, "reason"));
        return changes;
    }

    private static void AddChangeIfDifferent(List<AuditChangeDto> list, string field, string? before, string? after)
    {
        var beforeNorm = before?.Trim() ?? "-";
        var afterNorm = after?.Trim() ?? "-";
        if (string.Equals(beforeNorm, afterNorm, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        list.Add(new AuditChangeDto
        {
            Field = field,
            Before = beforeNorm,
            After = afterNorm
        });
    }

    private static string? ToStringOrNull(int? value)
    {
        return value?.ToString();
    }

    private static string? ToStringOrNull(bool? value)
    {
        return value?.ToString();
    }

    private static string NormalizeOperationId(string? raw)
    {
        var normalized = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return $"op_{Guid.NewGuid():N}";
        }

        return normalized.Length <= 120 ? normalized : normalized[..120];
    }
}
