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
        CancellationToken cancellationToken,
        bool throwOnFailure = false)
    {
        var filePath = GetPhysicalPath(pageImageUrl);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Görüntü dosyası sunucuda bulunamadı: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        var fileName = Path.GetFileName(filePath);
        var formFile = new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = ResolveContentType(fileName)
        };

        return await _aiService.DetectHotspotsAsync(formFile.ToUploadedFile(), pageId, throwOnFailure);
    }

    private static string ResolveContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpeg" => "image/jpeg",
            ".jpg" => "image/jpeg",
            _ => "image/png"
        };
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
