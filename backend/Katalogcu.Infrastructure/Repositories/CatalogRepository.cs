using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class CatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _context;

    public CatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Catalog>> GetPublicCatalogsAsync(CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .AsNoTracking()
            .Where(c => c.Status == "Published")
            .Include(c => c.Pages.OrderBy(p => p.PageNumber).Take(1))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Catalog>> GetPublicCatalogsByUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.Catalogs
            .AsNoTracking()
            .Where(c => c.Status == "Published" && c.UserId == userId);

        if (allowedCatalogIds is { Count: > 0 })
        {
            query = query.Where(c => allowedCatalogIds.Contains(c.Id));
        }

        return await query
            .Include(c => c.Pages.OrderBy(p => p.PageNumber).Take(1))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetPublishedCatalogIdsByUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> requestedCatalogIds,
        CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Status == "Published" && requestedCatalogIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Catalog?> GetOwnedCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs.FirstOrDefaultAsync(c => c.Id == catalogId && c.UserId == userId, cancellationToken);
    }

    public Task<bool> FolderExistsForUserAsync(Guid folderId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Folders.AnyAsync(f => f.Id == folderId && f.UserId == userId, cancellationToken);
    }

    public Task<int> CountCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs.CountAsync(c => c.UserId == userId, cancellationToken);
    }

    public Task<int> CountProductsByCatalogOwnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.Products.Include(p => p.Catalog).CountAsync(p => p.Catalog != null && p.Catalog.UserId == userId, cancellationToken);
    }

    public Task<int> CountPendingCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs
            .Where(c => c.UserId == userId)
            .CountAsync(c => c.Status == "Processing" || c.Status == "Pending" || c.Status == "Uploading", cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogRecentSummary>> GetRecentCatalogsByUserAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedDate)
            .Take(take)
            .Select(c => new CatalogRecentSummary
            {
                Id = c.Id,
                Name = c.Name,
                Status = c.Status,
                PartCount = _context.Products.Count(p => p.CatalogId == c.Id),
                CreatedDate = c.CreatedDate
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogTopViewedSummary>> GetTopViewedCatalogsByUserAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            SELECT
                c."Id",
                c."Name",
                COUNT(v."Id")::int AS "ViewCount",
                MAX(v."ViewedAtUtc") AS "LastViewedAtUtc"
            FROM "CatalogViews" v
            INNER JOIN "Catalogs" c ON c."Id" = v."CatalogId"
            WHERE c."UserId" = @userId
            GROUP BY c."Id", c."Name"
            ORDER BY COUNT(v."Id") DESC, MAX(v."ViewedAtUtc") DESC
            LIMIT @take
            """,
            cancellationToken);

        AddParameter(command, "userId", userId);
        AddParameter(command, "take", take <= 0 ? 5 : take);

        var result = new List<CatalogTopViewedSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CatalogTopViewedSummary
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                ViewCount = reader.GetInt32(2),
                LastViewedAtUtc = reader.GetDateTime(3)
            });
        }

        return result;
    }

    public Task<int> CountVisualEmbeddingCatalogItemsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.CatalogItems.CountAsync(ci => ci.Catalog.UserId == userId && ci.VisualEmbedding != null, cancellationToken);
    }

    public async Task<int> CountCatalogViewsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            SELECT COUNT(*)
            FROM "CatalogViews"
            WHERE "OwnerUserId" = @userId
            """,
            cancellationToken);

        AddParameter(command, "userId", userId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar == null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
    }

    public async Task<int> CountCatalogViewsByUserInRangeAsync(Guid userId, DateTime fromUtc, CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            SELECT COUNT(*)
            FROM "CatalogViews"
            WHERE "OwnerUserId" = @userId
              AND "ViewedAtUtc" >= @fromUtc
            """,
            cancellationToken);

        AddParameter(command, "userId", userId);
        AddParameter(command, "fromUtc", fromUtc);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar == null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
    }

    public async Task<int> CountUniqueCatalogViewersByUserInRangeAsync(Guid userId, DateTime fromUtc, CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            SELECT COUNT(DISTINCT "FingerprintHash")
            FROM "CatalogViews"
            WHERE "OwnerUserId" = @userId
              AND "ViewedAtUtc" >= @fromUtc
            """,
            cancellationToken);

        AddParameter(command, "userId", userId);
        AddParameter(command, "fromUtc", fromUtc);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar == null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
    }

    public async Task<int> CountStorefrontViewsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            SELECT COUNT(*)
            FROM "PublicStorefrontViews"
            WHERE "OwnerUserId" = @userId
            """,
            cancellationToken);

        AddParameter(command, "userId", userId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar == null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
    }

    public async Task<int> CountStorefrontViewsByUserInRangeAsync(Guid userId, DateTime fromUtc, CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            SELECT COUNT(*)
            FROM "PublicStorefrontViews"
            WHERE "OwnerUserId" = @userId
              AND "ViewedAtUtc" >= @fromUtc
            """,
            cancellationToken);

        AddParameter(command, "userId", userId);
        AddParameter(command, "fromUtc", fromUtc);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar == null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
    }

    public async Task<int> CountUniqueStorefrontVisitorsByUserInRangeAsync(Guid userId, DateTime fromUtc, CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            SELECT COUNT(DISTINCT "FingerprintHash")
            FROM "PublicStorefrontViews"
            WHERE "OwnerUserId" = @userId
              AND "ViewedAtUtc" >= @fromUtc
            """,
            cancellationToken);

        AddParameter(command, "userId", userId);
        AddParameter(command, "fromUtc", fromUtc);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar == null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
    }

    public async Task<bool> RecordCatalogViewAsync(
        Guid catalogId,
        Guid ownerUserId,
        string fingerprintHash,
        DateTime bucketStartUtc,
        DateTime viewedAtUtc,
        string source,
        CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            INSERT INTO "CatalogViews"
                ("Id", "CatalogId", "OwnerUserId", "FingerprintHash", "BucketStartUtc", "ViewedAtUtc", "Source", "CreatedDate")
            VALUES
                (@id, @catalogId, @ownerUserId, @fingerprintHash, @bucketStartUtc, @viewedAtUtc, @source, @createdDate)
            ON CONFLICT ("CatalogId", "FingerprintHash", "BucketStartUtc")
            DO NOTHING
            """,
            cancellationToken);

        AddParameter(command, "id", Guid.NewGuid());
        AddParameter(command, "catalogId", catalogId);
        AddParameter(command, "ownerUserId", ownerUserId);
        AddParameter(command, "fingerprintHash", fingerprintHash);
        AddParameter(command, "bucketStartUtc", bucketStartUtc);
        AddParameter(command, "viewedAtUtc", viewedAtUtc);
        AddParameter(command, "source", source);
        AddParameter(command, "createdDate", DateTime.UtcNow);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<bool> RecordStorefrontViewAsync(
        Guid ownerUserId,
        string fingerprintHash,
        DateTime bucketStartUtc,
        DateTime viewedAtUtc,
        string source,
        CancellationToken cancellationToken)
    {
        using var command = await CreateCommandAsync(
            """
            INSERT INTO "PublicStorefrontViews"
                ("Id", "OwnerUserId", "FingerprintHash", "BucketStartUtc", "ViewedAtUtc", "Source", "CreatedDate")
            VALUES
                (@id, @ownerUserId, @fingerprintHash, @bucketStartUtc, @viewedAtUtc, @source, @createdDate)
            ON CONFLICT ("OwnerUserId", "FingerprintHash", "BucketStartUtc")
            DO NOTHING
            """,
            cancellationToken);

        AddParameter(command, "id", Guid.NewGuid());
        AddParameter(command, "ownerUserId", ownerUserId);
        AddParameter(command, "fingerprintHash", fingerprintHash);
        AddParameter(command, "bucketStartUtc", bucketStartUtc);
        AddParameter(command, "viewedAtUtc", viewedAtUtc);
        AddParameter(command, "source", source);
        AddParameter(command, "createdDate", DateTime.UtcNow);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<IReadOnlyList<Catalog>> GetCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .Where(c => c.UserId == userId)
            .Include(c => c.Pages)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Catalog?> GetCatalogByIdForAccessAsync(
        Guid catalogId,
        Guid userId,
        bool publicOnlyPublished,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.Catalogs
            .Include(c => c.Pages.OrderBy(p => p.PageNumber))
            .ThenInclude(p => p.Hotspots)
            .Where(c => c.Id == catalogId && c.UserId == userId);

        if (publicOnlyPublished)
        {
            query = query.Where(c => c.Status == "Published");
            if (allowedCatalogIds is { Count: > 0 })
            {
                query = query.Where(c => allowedCatalogIds.Contains(c.Id));
            }
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItem>> GetCatalogItemsForPageAsync(
        Guid catalogId,
        string pageNumber,
        Guid userId,
        bool publicOnlyPublished,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.CatalogItems
            .Include(ci => ci.Catalog)
            .AsNoTracking()
            .Where(ci => ci.CatalogId == catalogId && ci.PageNumber == pageNumber && ci.Catalog.UserId == userId);

        if (publicOnlyPublished)
        {
            query = query.Where(ci => ci.Catalog.Status == "Published");
            if (allowedCatalogIds is { Count: > 0 })
            {
                query = query.Where(ci => allowedCatalogIds.Contains(ci.CatalogId));
            }
        }

        return await query.OrderBy(ci => ci.RefNumber).ToListAsync(cancellationToken);
    }

    public Task<CatalogItem?> GetCatalogItemByIdForUserAsync(
        Guid catalogItemId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _context.CatalogItems
            .Include(ci => ci.Catalog)
            .FirstOrDefaultAsync(
                ci => ci.Id == catalogItemId && ci.Catalog.UserId == userId,
                cancellationToken);
    }

    public Task AddCatalogItemAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        return _context.CatalogItems.AddAsync(item, cancellationToken).AsTask();
    }

    public void RemoveCatalogItem(CatalogItem item)
    {
        _context.CatalogItems.Remove(item);
    }

    public Task<bool> CatalogPageExistsForCatalogAsync(
        Guid catalogId,
        int pageNumber,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _context.CatalogPages
            .Include(p => p.Catalog)
            .AnyAsync(
                p => p.CatalogId == catalogId &&
                     p.PageNumber == pageNumber &&
                     p.Catalog != null &&
                     p.Catalog.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Product>> GetOwnedStockedProductsByCodesAsync(
        Guid userId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return new Dictionary<string, Product>();
        }

        return await _context.Products
            .Include(p => p.Catalog)
            .AsNoTracking()
            .Where(p => codes.Contains(p.Code) && p.Catalog != null && p.Catalog.UserId == userId)
            .GroupBy(p => p.Code)
            .Select(g => g.First())
            .ToDictionaryAsync(p => p.Code, cancellationToken);
    }

    public Task AddCatalogAsync(Catalog catalog, CancellationToken cancellationToken)
    {
        return _context.Catalogs.AddAsync(catalog, cancellationToken).AsTask();
    }

    public Task AddCatalogPagesAsync(IEnumerable<CatalogPage> pages, CancellationToken cancellationToken)
    {
        return _context.CatalogPages.AddRangeAsync(pages, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetProductIdsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(p => p.CatalogId == catalogId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteOrderItemsByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return Task.CompletedTask;
        return _context.OrderItems.Where(oi => productIds.Contains(oi.ProductId)).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteHotspotsByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return Task.CompletedTask;
        return _context.Hotspots.Where(h => h.ProductId.HasValue && productIds.Contains(h.ProductId.Value)).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteProductsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.Products.Where(p => p.CatalogId == catalogId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteCatalogItemsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.CatalogItems.Where(ci => ci.CatalogId == catalogId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteCatalogPagesByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.CatalogPages.Where(cp => cp.CatalogId == catalogId).ExecuteDeleteAsync(cancellationToken);
    }

    public void RemoveCatalog(Catalog catalog)
    {
        _context.Catalogs.Remove(catalog);
    }

    public Task<CatalogPage?> GetCatalogPageByIdAsync(Guid pageId, CancellationToken cancellationToken)
    {
        return _context.CatalogPages.FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);
    }

    public Task DeleteHotspotsByPageIdAsync(Guid pageId, CancellationToken cancellationToken)
    {
        return _context.Hotspots.Where(h => h.PageId == pageId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteCatalogItemsByCatalogAndPageNumberAsync(Guid catalogId, string pageNumber, CancellationToken cancellationToken)
    {
        return _context.CatalogItems
            .Where(ci => ci.CatalogId == catalogId && ci.PageNumber == pageNumber)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<DbCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;
        var tx = _context.Database.CurrentTransaction;
        if (tx != null)
        {
            command.Transaction = tx.GetDbTransaction();
        }

        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
