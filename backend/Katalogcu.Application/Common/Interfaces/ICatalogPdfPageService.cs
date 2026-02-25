namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogPdfPageService
{
    Task<IReadOnlyList<string>> ConvertCatalogPdfToPageImageUrlsAsync(string pdfUrl, CancellationToken cancellationToken);
}
