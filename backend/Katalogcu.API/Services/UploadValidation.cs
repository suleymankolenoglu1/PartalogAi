using Microsoft.AspNetCore.Http;

namespace Katalogcu.API.Services;

public static class UploadValidation
{
    public const long MaxImageBytes = 10L * 1024 * 1024;
    public const long MaxPdfBytes = 50L * 1024 * 1024;
    public const long MaxSpreadsheetBytes = 25L * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly HashSet<string> PdfContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf"
    };

    private static readonly HashSet<string> SpreadsheetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".csv"
    };

    private static readonly HashSet<string> SpreadsheetContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "text/csv",
        "application/csv",
        "text/plain"
    };

    public static string? ValidateImage(IFormFile? file, bool required = true, long maxBytes = MaxImageBytes)
    {
        return ValidateByRule(file, required, maxBytes, ImageExtensions, ImageContentTypes, "Görsel");
    }

    public static string? ValidatePdf(IFormFile? file, bool required = true, long maxBytes = MaxPdfBytes)
    {
        return ValidateByRule(file, required, maxBytes, PdfExtensions, PdfContentTypes, "PDF");
    }

    public static string? ValidateSpreadsheet(IFormFile? file, bool required = true, bool allowCsv = true, long maxBytes = MaxSpreadsheetBytes)
    {
        var allowedExtensions = allowCsv
            ? SpreadsheetExtensions
            : new HashSet<string>(new[] { ".xlsx" }, StringComparer.OrdinalIgnoreCase);

        return ValidateByRule(file, required, maxBytes, allowedExtensions, SpreadsheetContentTypes, "Excel/CSV");
    }

    public static string? ValidateExternalSiteImportFile(IFormFile? file)
    {
        return ValidateSpreadsheet(file, required: true, allowCsv: true, maxBytes: MaxSpreadsheetBytes);
    }

    public static string? ValidateUploadFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return "Lütfen bir dosya seçin.";
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ImageExtensions.Contains(extension))
        {
            return ValidateImage(file, required: true);
        }

        if (PdfExtensions.Contains(extension))
        {
            return ValidatePdf(file, required: true);
        }

        return "Desteklenmeyen dosya türü. Sadece JPG, PNG, WEBP veya PDF yükleyebilirsiniz.";
    }

    public static string SanitizeFileName(string? fileName)
    {
        return Path.GetFileName(fileName ?? string.Empty);
    }

    private static string? ValidateByRule(
        IFormFile? file,
        bool required,
        long maxBytes,
        HashSet<string> allowedExtensions,
        HashSet<string> allowedContentTypes,
        string label)
    {
        if (file == null || file.Length == 0)
        {
            return required ? "Lütfen bir dosya seçin." : null;
        }

        if (file.Length > maxBytes)
        {
            var maxMb = maxBytes / (1024 * 1024);
            return $"{label} dosyası en fazla {maxMb} MB olabilir.";
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            return $"{label} dosya uzantısı geçersiz.";
        }

        var contentType = (file.ContentType ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(contentType) &&
            !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
            !allowedContentTypes.Contains(contentType))
        {
            return $"{label} içerik tipi geçersiz.";
        }

        return null;
    }
}
