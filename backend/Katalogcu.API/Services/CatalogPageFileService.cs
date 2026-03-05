using System.Net;
using Katalogcu.Application.Common.Interfaces;

namespace Katalogcu.API.Services;

public sealed class CatalogPageFileService : ICatalogPageFileService
{
    private readonly IWebHostEnvironment _env;

    public CatalogPageFileService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<byte[]?> ReadImageBytesAsync(string imageUrl, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(imageUrl);
        if (fullPath == null || !File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    private string? GetFullPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return null;
        }

        var cleanPath = WebUtility.UrlDecode(url);
        if (Uri.TryCreate(cleanPath, UriKind.Absolute, out var uri))
        {
            cleanPath = uri.LocalPath;
        }

        cleanPath = cleanPath
            .TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(cleanPath))
        {
            return null;
        }

        var combined = Path.GetFullPath(Path.Combine(webRoot, cleanPath));
        var rootWithSep = Path.GetFullPath(webRoot + Path.DirectorySeparatorChar);
        if (!combined.StartsWith(rootWithSep, StringComparison.Ordinal))
        {
            return null;
        }

        return combined;
    }
}
