using Katalogcu.Application.Common.Interfaces;

namespace Katalogcu.API.Services;

public sealed class CatalogPdfPageService : ICatalogPdfPageService
{
    private readonly PdfService _pdfService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CatalogPdfPageService(PdfService pdfService, IHttpContextAccessor httpContextAccessor)
    {
        _pdfService = pdfService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<string>> ConvertCatalogPdfToPageImageUrlsAsync(string pdfUrl, CancellationToken cancellationToken)
    {
        var fileName = ExtractFileName(pdfUrl);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return [];
        }

        var pagePaths = await _pdfService.ConvertPdfToImagesAsync(fileName, cancellationToken);
        if (pagePaths.Count == 0)
        {
            return [];
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            return pagePaths;
        }

        var baseUrl = $"{request.Scheme}://{request.Host}";
        return pagePaths.Select(path => $"{baseUrl}/{path.TrimStart('/')}").ToList();
    }

    private static string ExtractFileName(string pdfUrl)
    {
        if (Uri.TryCreate(pdfUrl, UriKind.Absolute, out var uri))
        {
            return Path.GetFileName(uri.LocalPath);
        }

        return Path.GetFileName(pdfUrl);
    }
}
