using Katalogcu.API.Services;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Controllers;

[ApiController]
[Route("api/catalog-pages")]
public sealed class CatalogPagesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;
    private readonly ICatalogPageAccessTokenService _catalogPageAccessTokenService;

    public CatalogPagesController(
        AppDbContext dbContext,
        IFileStorageService fileStorage,
        ICatalogPageAccessTokenService catalogPageAccessTokenService)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _catalogPageAccessTokenService = catalogPageAccessTokenService;
    }

    [HttpGet("{pageId:guid}/image")]
    public async Task<IActionResult> GetImage(
        Guid pageId,
        [FromQuery] string accessToken,
        CancellationToken cancellationToken)
    {
        var payload = _catalogPageAccessTokenService.Validate(accessToken);
        if (payload == null || payload.PageId != pageId)
        {
            return NotFound();
        }

        var page = await _dbContext.CatalogPages
            .AsNoTracking()
            .Where(x => x.Id == pageId)
            .Select(x => new
            {
                x.Id,
                x.CatalogId,
                x.ImageUrl,
                OwnerUserId = x.Catalog!.UserId,
                CatalogStatus = x.Catalog!.Status,
                PublicLinkEnabled = x.Catalog!.User!.PublicLinkEnabled,
                OwnerRole = x.Catalog!.User!.Role
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (page == null ||
            page.CatalogId != payload.CatalogId ||
            page.OwnerUserId != payload.OwnerUserId ||
            string.IsNullOrWhiteSpace(page.ImageUrl))
        {
            return NotFound();
        }

        if (payload.IsPublic)
        {
            if (!string.Equals(page.CatalogStatus, "Published", StringComparison.OrdinalIgnoreCase) ||
                !page.PublicLinkEnabled ||
                string.Equals(page.OwnerRole, "SuspendedOwner", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            if (payload.AllowedCatalogIds.Count > 0 && !payload.AllowedCatalogIds.Contains(page.CatalogId))
            {
                return NotFound();
            }
        }

        if (!_fileStorage.TryGetObjectPath(page.ImageUrl, out var objectPath))
        {
            return NotFound();
        }

        var storedFile = await _fileStorage.OpenReadAsync(objectPath, cancellationToken);
        if (storedFile == null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=300";
        return File(storedFile.Stream, storedFile.ContentType ?? "application/octet-stream", enableRangeProcessing: true);
    }
}
