using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Katalogcu.API.Services;

public sealed class ExcludedStaticFileProvider : IFileProvider
{
    private readonly IFileProvider _innerProvider;
    private readonly string _excludedRoot;

    public ExcludedStaticFileProvider(IFileProvider innerProvider, string excludedRoot)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _excludedRoot = NormalizePath(excludedRoot);

        if (string.IsNullOrWhiteSpace(_excludedRoot))
        {
            throw new ArgumentException("Excluded root cannot be empty.", nameof(excludedRoot));
        }
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        if (IsExcluded(subpath))
        {
            return NotFoundDirectoryContents.Singleton;
        }

        return _innerProvider.GetDirectoryContents(subpath);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        if (IsExcluded(subpath))
        {
            return new NotFoundFileInfo(Path.GetFileName(subpath));
        }

        return _innerProvider.GetFileInfo(subpath);
    }

    public IChangeToken Watch(string filter)
    {
        if (IsExcluded(filter))
        {
            return NullChangeToken.Singleton;
        }

        return _innerProvider.Watch(filter);
    }

    private bool IsExcluded(string? subpath)
    {
        var normalized = NormalizePath(subpath);
        return normalized.Equals(_excludedRoot, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(_excludedRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? path)
    {
        return (path ?? string.Empty)
            .Replace('\\', '/')
            .Trim()
            .TrimStart('~')
            .TrimStart('/');
    }
}
