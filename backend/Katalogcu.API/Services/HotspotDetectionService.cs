using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.API.Services;

public sealed class HotspotDetectionService : IHotspotDetectionService
{
    private readonly IPartalogAiService _aiService;
    private readonly IWebHostEnvironment _env;

    public HotspotDetectionService(IPartalogAiService aiService, IWebHostEnvironment env)
    {
        _aiService = aiService;
        _env = env;
    }

    public async Task<IReadOnlyList<Hotspot>> DetectHotspotsForPageAsync(
        string pageImageUrl,
        Guid pageId,
        CancellationToken cancellationToken)
    {
        var filePath = GetPhysicalPath(pageImageUrl);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Görüntü dosyası sunucuda bulunamadı: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        var formFile = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(filePath))
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        return await _aiService.DetectHotspotsAsync(formFile, pageId);
    }

    private string GetPhysicalPath(string url)
    {
        var fileName = Path.GetFileName(url);

        var pathPages = Path.Combine(_env.WebRootPath, "uploads", "pages", fileName);
        if (File.Exists(pathPages)) return pathPages;

        var pathRoot = Path.Combine(_env.WebRootPath, "uploads", fileName);
        if (File.Exists(pathRoot)) return pathRoot;

        return pathPages;
    }
}
