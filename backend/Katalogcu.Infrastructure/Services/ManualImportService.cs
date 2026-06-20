using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using Katalogcu.Domain.Entities;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Katalogcu.Infrastructure.Services;

public sealed class ManualImportService : IManualImportService
{
    private const int MaxRowCount = 50000;
    private const int BatchSize = 1000;

    private readonly IExternalSiteRepository _externalSiteRepository;
    private readonly IManualImportFileRepository _manualImportFileRepository;
    private readonly IExternalProductNormalizer _externalProductNormalizer;
    private readonly IExternalProductUpsertService _externalProductUpsertService;
    private readonly ILogger<ManualImportService> _logger;

    public ManualImportService(
        IExternalSiteRepository externalSiteRepository,
        IManualImportFileRepository manualImportFileRepository,
        IExternalProductNormalizer externalProductNormalizer,
        IExternalProductUpsertService externalProductUpsertService,
        ILogger<ManualImportService> logger)
    {
        _externalSiteRepository = externalSiteRepository;
        _manualImportFileRepository = manualImportFileRepository;
        _externalProductNormalizer = externalProductNormalizer;
        _externalProductUpsertService = externalProductUpsertService;
        _logger = logger;
    }

    public async Task<ManualImportResultDto> ImportFromFileAsync(
        UploadedFile file,
        Guid externalSiteId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var site = await _externalSiteRepository.GetSiteByIdAsync(externalSiteId, userId, cancellationToken);
        if (site is null)
        {
            throw new InvalidOperationException("Site kaydı bulunamadı.");
        }

        var resolvedFileType = ResolveFileType(file);
        var storagePath = await SaveImportFileAsync(file, resolvedFileType, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        var importFile = new ManualImportFile
        {
            Id = Guid.NewGuid(),
            ExternalSiteId = externalSiteId,
            FileName = file.FileName,
            FileType = resolvedFileType,
            StoragePath = storagePath,
            ImportedAtUtc = nowUtc,
            ImportedByUserId = userId,
            RowCount = 0,
            Status = "running",
            CreatedDate = nowUtc
        };

        var crawl = new ExternalSiteCrawl
        {
            Id = Guid.NewGuid(),
            ExternalSiteId = externalSiteId,
            TriggerType = "manual",
            ExecutionMode = $"manual_{resolvedFileType}",
            Status = "running",
            StartedAtUtc = nowUtc,
            CreatedDate = nowUtc
        };

        await _manualImportFileRepository.AddAsync(importFile, cancellationToken);
        await _externalSiteRepository.AddCrawlAsync(crawl, cancellationToken);

        site.LastCrawlAtUtc = nowUtc;
        site.UpdatedDate = nowUtc;

        await _manualImportFileRepository.SaveChangesAsync(cancellationToken);

        try
        {
            var batch = new List<CrawledProduct>(BatchSize);
            var seenSourceUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowCount = 0;
            var importedProductCount = 0;
            var failedRowCount = 0;
            var rowsWithSku = 0;
            var rowsWithOem = 0;

            await foreach (var product in ParseProductsAsync(file, resolvedFileType, site.BaseUrl, cancellationToken))
            {
                rowCount++;
                if (rowCount > MaxRowCount)
                {
                    throw new InvalidOperationException($"Dosya en fazla {MaxRowCount:N0} satır içerebilir.");
                }

                if (product is null)
                {
                    failedRowCount++;
                    continue;
                }

                batch.Add(product);
                seenSourceUrls.Add(product.SourceUrl);

                if (!string.IsNullOrWhiteSpace(product.Sku) || !string.IsNullOrWhiteSpace(product.PartCode))
                {
                    rowsWithSku++;
                }

                if (product.OemNumbers.Count > 0)
                {
                    rowsWithOem++;
                }

                if (batch.Count >= BatchSize)
                {
                    importedProductCount += await FlushBatchAsync(externalSiteId, crawl.Id, batch, cancellationToken);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                importedProductCount += await FlushBatchAsync(externalSiteId, crawl.Id, batch, cancellationToken);
                batch.Clear();
            }

            if (seenSourceUrls.Count > 0)
            {
                await _externalProductUpsertService.MarkMissingInactiveAsync(externalSiteId, seenSourceUrls, cancellationToken);
            }

            importFile.RowCount = rowCount;
            importFile.Status = "completed";
            importFile.ErrorSummary = null;
            importFile.UpdatedDate = DateTime.UtcNow;

            crawl.ProductCount = importedProductCount;
            crawl.SkuCoverage = CalculateCoverage(rowCount, rowsWithSku);
            crawl.OemCoverage = CalculateCoverage(rowCount, rowsWithOem);
            crawl.Status = "completed";
            crawl.ErrorSummary = null;
            crawl.CompletedAtUtc = DateTime.UtcNow;
            crawl.UpdatedDate = crawl.CompletedAtUtc;

            site.LastSuccessfulCrawlAtUtc = crawl.CompletedAtUtc;
            site.UpdatedDate = DateTime.UtcNow;

            await _manualImportFileRepository.SaveChangesAsync(cancellationToken);

            return new ManualImportResultDto
            {
                ManualImportFileId = importFile.Id,
                SiteId = externalSiteId,
                RowCount = rowCount,
                ImportedProductCount = importedProductCount,
                FailedRowCount = failedRowCount,
                FileType = resolvedFileType,
                Status = importFile.Status,
                ErrorSummary = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual import başarısız oldu: {ExternalSiteId} | File={FileName}", externalSiteId, file.FileName);

            importFile.Status = "failed";
            importFile.ErrorSummary = Truncate(ex.Message, 2048);
            importFile.UpdatedDate = DateTime.UtcNow;

            crawl.Status = "failed";
            crawl.ErrorSummary = Truncate(ex.Message, 2048);
            crawl.CompletedAtUtc = DateTime.UtcNow;
            crawl.UpdatedDate = crawl.CompletedAtUtc;

            site.UpdatedDate = DateTime.UtcNow;

            await _manualImportFileRepository.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> FlushBatchAsync(
        Guid externalSiteId,
        Guid crawlId,
        IReadOnlyList<CrawledProduct> batch,
        CancellationToken cancellationToken)
    {
        var normalized = _externalProductNormalizer.Normalize(externalSiteId, crawlId, batch);
        return await _externalProductUpsertService.UpsertAsync(externalSiteId, crawlId, normalized, cancellationToken);
    }

    private async IAsyncEnumerable<CrawledProduct?> ParseProductsAsync(
        UploadedFile file,
        string fileType,
        string baseUrl,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var row in fileType switch
        {
            "csv" => ParseCsvRows(file),
            "xml" => ParseXmlRows(file),
            _ => ParseXlsxRows(file)
        })
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return NormalizeRowToProduct(row, baseUrl);
        }

        await Task.CompletedTask;
    }

    private IReadOnlyList<Dictionary<string, string>> ParseCsvRows(UploadedFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return [];
        }

        var delimiter = DetectDelimiter(headerLine);
        var headers = ParseDelimitedLine(headerLine, delimiter);
        var rows = new List<Dictionary<string, string>>();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseDelimitedLine(line, delimiter);
            rows.Add(MapRow(headers, values));
        }

        return rows;
    }

    private IReadOnlyList<Dictionary<string, string>> ParseXlsxRows(UploadedFile file)
    {
        using var stream = file.OpenReadStream();
        stream.Position = 0;

        var workbook = new XSSFWorkbook(stream);
        var sheet = workbook.GetSheetAt(0);
        var formatter = new DataFormatter(CultureInfo.InvariantCulture);
        var headerRow = sheet.GetRow(sheet.FirstRowNum);
        if (headerRow is null)
        {
            return [];
        }

        var headers = new List<string>();
        for (var i = headerRow.FirstCellNum; i < headerRow.LastCellNum; i++)
        {
            headers.Add(formatter.FormatCellValue(headerRow.GetCell(i)));
        }

        var rows = new List<Dictionary<string, string>>();
        for (var i = sheet.FirstRowNum + 1; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row is null)
            {
                continue;
            }

            var values = new List<string>();
            for (var cellIndex = 0; cellIndex < headers.Count; cellIndex++)
            {
                values.Add(formatter.FormatCellValue(row.GetCell(cellIndex)));
            }

            rows.Add(MapRow(headers, values));
        }

        return rows;
    }

    private IReadOnlyList<Dictionary<string, string>> ParseXmlRows(UploadedFile file)
    {
        using var stream = file.OpenReadStream();
        var document = XDocument.Load(stream);
        var candidates = document
            .Descendants()
            .Where(x => x.HasElements)
            .Where(x => x.Name.LocalName.Equals("product", StringComparison.OrdinalIgnoreCase)
                     || x.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase)
                     || x.Name.LocalName.Equals("row", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = document.Root?.Elements().Where(x => x.HasElements).ToList() ?? [];
        }

        return candidates
            .Select(node => node.Elements()
                .GroupBy(x => x.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(" | ", g.Select(x => x.Value.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))),
                    StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private CrawledProduct? NormalizeRowToProduct(IReadOnlyDictionary<string, string> row, string baseUrl)
    {
        var title = GetFirst(row, "title", "urunadi", "urunadiad", "productname", "name", "ad", "partname", "parcaadi");
        var sku = GetFirst(row, "sku", "code", "kod", "productcode", "urunkodu", "stokkodu");
        var partCode = GetFirst(row, "partcode", "parcakodu", "itemcode");
        var effectiveCode = FirstNonEmpty(partCode, sku);

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(effectiveCode))
        {
            return null;
        }

        var url = GetFirst(row, "url", "sourceurl", "link", "producturl", "urunurl");
        var brand = GetFirst(row, "brand", "marka");
        var category = GetFirst(row, "category", "kategori");
        var oemValue = GetFirst(row, "oemnumber", "oem", "oemno", "oemnumbers");
        var categoryPath = SplitCompositeValue(category);
        var oemNumbers = SplitCompositeValue(oemValue);
        var sourceUrl = NormalizeImportUrl(url, baseUrl, effectiveCode, title, brand, category);

        return new CrawledProduct
        {
            SourceUrl = sourceUrl,
            CanonicalUrl = sourceUrl,
            Title = TrimOrNull(title),
            Sku = TrimOrNull(sku),
            PartCode = TrimOrNull(effectiveCode),
            Brand = TrimOrNull(brand),
            CategoryPath = categoryPath,
            OemNumbers = oemNumbers,
            RawPayloadJson = System.Text.Json.JsonSerializer.Serialize(row)
        };
    }

    private static string ResolveFileType(UploadedFile file)
    {
        var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (extension is "xlsx" or "csv" or "xml")
        {
            return extension;
        }

        return file.ContentType switch
        {
            "text/csv" => "csv",
            "application/xml" => "xml",
            "text/xml" => "xml",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "xlsx",
            _ => throw new InvalidOperationException("Desteklenen dosya tipleri: xlsx, csv, xml.")
        };
    }

    private static async Task<string> SaveImportFileAsync(UploadedFile file, string fileType, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "katalogcu", "manual-imports");
        Directory.CreateDirectory(directory);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.{fileType}";
        var fullPath = Path.Combine(directory, fileName);

        await using var targetStream = File.Create(fullPath);
        await file.CopyToAsync(targetStream, cancellationToken);
        return fullPath;
    }

    private static Dictionary<string, string> MapRow(IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = NormalizeHeader(headers[i]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            row[key] = i < values.Count ? values[i].Trim() : string.Empty;
        }

        return row;
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values;
    }

    private static char DetectDelimiter(string headerLine)
    {
        if (headerLine.Count(ch => ch == ';') > headerLine.Count(ch => ch == ','))
        {
            return ';';
        }

        return ',';
    }

    private static string NormalizeImportUrl(string? url, string baseUrl, string? code, string? title, string? brand, string? category)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var absolute))
            {
                return absolute.ToString().TrimEnd('/');
            }

            return new Uri(new Uri(baseUrl), url.Trim()).ToString().TrimEnd('/');
        }

        var key = FirstNonEmpty(code, $"{title}-{brand}-{category}", title, Guid.NewGuid().ToString("N"))!;
        var slug = Slugify(key);
        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), $"manual-import/{slug}").ToString().TrimEnd('/');
    }

    private static IReadOnlyList<string> SplitCompositeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(['|', ',', ';', '>'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetFirst(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(NormalizeHeader(key), out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string NormalizeHeader(string value)
    {
        var lowered = value.Trim().ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ö', 'o')
            .Replace('ç', 'c');

        return new string(lowered.Where(char.IsLetterOrDigit).ToArray());
    }

    private static string Slugify(string value)
    {
        var normalized = NormalizeHeader(value);
        return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("N") : normalized[..Math.Min(normalized.Length, 120)];
    }

    private static decimal CalculateCoverage(int totalRows, int coveredRows)
    {
        if (totalRows <= 0)
        {
            return 0;
        }

        return Math.Round((decimal)coveredRows / totalRows * 100m, 2);
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
