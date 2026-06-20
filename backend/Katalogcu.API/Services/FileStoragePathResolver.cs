using System.Net;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class FileStoragePathResolver
{
    private readonly FileStorageOptions _options;

    public FileStoragePathResolver(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
    }

    public bool TryGetObjectPath(string? urlOrPath, out string objectPath)
    {
        objectPath = string.Empty;
        if (string.IsNullOrWhiteSpace(urlOrPath))
        {
            return false;
        }

        var value = WebUtility.UrlDecode(urlOrPath).Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            value = StripKnownAbsoluteUri(uri, value);
        }

        var queryIndex = value.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            value = value[..queryIndex];
        }

        value = value.TrimStart('/', '\\').Replace('\\', '/');

        const string downloadPrefix = "api/files/download/";
        if (value.StartsWith(downloadPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(value[downloadPrefix.Length..]);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            objectPath = FileStoragePath.NormalizeObjectPath($"uploads/{fileName}");
            return true;
        }

        if (!value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        objectPath = FileStoragePath.NormalizeObjectPath(value);
        return true;
    }

    private string StripKnownAbsoluteUri(Uri uri, string originalValue)
    {
        var publicBaseUrl = _options.PublicBaseUrl?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl) &&
            originalValue.StartsWith(publicBaseUrl + "/", StringComparison.OrdinalIgnoreCase))
        {
            return originalValue[(publicBaseUrl.Length + 1)..];
        }

        var localPath = uri.LocalPath.TrimStart('/');
        var bucketName = _options.BucketName?.Trim();
        if (!string.IsNullOrWhiteSpace(bucketName))
        {
            if (uri.Host.Equals("storage.googleapis.com", StringComparison.OrdinalIgnoreCase) &&
                localPath.StartsWith(bucketName + "/", StringComparison.OrdinalIgnoreCase))
            {
                return localPath[(bucketName.Length + 1)..];
            }

            if (uri.Host.Equals($"{bucketName}.storage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                return localPath;
            }
        }

        return uri.LocalPath;
    }
}
