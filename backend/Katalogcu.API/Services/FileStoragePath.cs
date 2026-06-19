using System.Net;

namespace Katalogcu.API.Services;

public static class FileStoragePath
{
    public static string NormalizeObjectPath(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            throw new ArgumentException("Object path is required.", nameof(objectPath));
        }

        var normalized = WebUtility.UrlDecode(objectPath)
            .Trim()
            .TrimStart('/', '\\')
            .Replace('\\', '/');

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Object path is invalid.", nameof(objectPath));
        }

        return string.Join('/', segments);
    }
}
