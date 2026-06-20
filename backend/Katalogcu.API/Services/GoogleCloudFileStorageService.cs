using Google;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class GoogleCloudFileStorageService : IFileStorageService
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly FileStorageOptions _options;
    private readonly FileStoragePathResolver _pathResolver;
    private readonly StorageClient _client;

    public GoogleCloudFileStorageService(
        IOptions<FileStorageOptions> options,
        FileStoragePathResolver pathResolver)
    {
        _options = options.Value;
        _pathResolver = pathResolver;
        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException("FileStorage:BucketName is required for GoogleCloudStorage provider.");
        }

        _client = StorageClient.Create();
    }

    public async Task SaveAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var normalized = FileStoragePath.NormalizeObjectPath(objectPath);
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await _client.UploadObjectAsync(
            _options.BucketName,
            normalized,
            contentType,
            content,
            cancellationToken: cancellationToken);
    }

    public async Task<StoredFile?> OpenReadAsync(string objectPath, CancellationToken cancellationToken)
    {
        var normalized = FileStoragePath.NormalizeObjectPath(objectPath);
        var memory = new MemoryStream();
        try
        {
            await _client.DownloadObjectAsync(
                _options.BucketName,
                normalized,
                memory,
                cancellationToken: cancellationToken);
        }
        catch (GoogleApiException ex) when (ex.Error?.Code == StatusCodes.Status404NotFound)
        {
            await memory.DisposeAsync();
            return null;
        }

        memory.Position = 0;
        if (!ContentTypeProvider.TryGetContentType(normalized, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return new StoredFile(memory, contentType);
    }

    public async Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken)
    {
        var normalized = FileStoragePath.NormalizeObjectPath(objectPath);
        try
        {
            await _client.GetObjectAsync(_options.BucketName, normalized, cancellationToken: cancellationToken);
            return true;
        }
        catch (GoogleApiException ex) when (ex.Error?.Code == StatusCodes.Status404NotFound)
        {
            return false;
        }
    }

    public string? GetPublicUrl(string objectPath)
    {
        var normalized = FileStoragePath.NormalizeObjectPath(objectPath);
        var baseUrl = _options.PublicBaseUrl?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"{baseUrl}/{Uri.EscapeDataString(normalized).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)}";
        }

        return $"https://storage.googleapis.com/{_options.BucketName}/{Uri.EscapeDataString(normalized).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)}";
    }

    public bool TryGetObjectPath(string? urlOrPath, out string objectPath)
    {
        return _pathResolver.TryGetObjectPath(urlOrPath, out objectPath);
    }
}
