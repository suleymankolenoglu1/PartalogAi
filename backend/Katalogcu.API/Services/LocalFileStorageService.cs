using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly string _rootPath;
    private readonly FileStoragePathResolver _pathResolver;

    public LocalFileStorageService(
        IWebHostEnvironment env,
        IOptions<FileStorageOptions> options,
        FileStoragePathResolver pathResolver)
    {
        _pathResolver = pathResolver;
        var configuredRoot = options.Value.LocalRootPath;
        _rootPath = string.IsNullOrWhiteSpace(configuredRoot)
            ? env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : configuredRoot;
    }

    public async Task SaveAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(objectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<StoredFile?> OpenReadAsync(string objectPath, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(objectPath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        if (!ContentTypeProvider.TryGetContentType(fullPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<StoredFile?>(new StoredFile(stream, contentType));
    }

    public Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken)
    {
        return Task.FromResult(File.Exists(GetFullPath(objectPath)));
    }

    public string? GetPublicUrl(string objectPath)
    {
        return FileStoragePath.NormalizeObjectPath(objectPath);
    }

    public bool TryGetObjectPath(string? urlOrPath, out string objectPath)
    {
        return _pathResolver.TryGetObjectPath(urlOrPath, out objectPath);
    }

    private string GetFullPath(string objectPath)
    {
        var normalized = FileStoragePath.NormalizeObjectPath(objectPath)
            .Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var rootWithSeparator = Path.GetFullPath(_rootPath + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage path escapes the configured root.");
        }

        return fullPath;
    }
}
