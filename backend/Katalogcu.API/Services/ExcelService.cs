using System.Globalization;
using System.Text;
using Katalogcu.Domain.Entities;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Katalogcu.API.Services
{
    public class ExcelService
    {
        public sealed class StockImportRow
        {
            public int RowNumber { get; init; }
            public string Code { get; init; } = string.Empty;
            public int? StockQuantity { get; init; }
            public decimal? Price { get; init; }
            public string? Name { get; init; }
            public string? Category { get; init; }
            public string? Description { get; init; }
        }

        public List<Product> ParseProducts(IFormFile file, Guid catalogId)
        {
            var products = new List<Product>();
            var formatter = new DataFormatter(CultureInfo.InvariantCulture);

            using var stream = file.OpenReadStream();
            stream.Position = 0;

            var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0);

            for (int i = sheet.FirstRowNum + 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                try
                {
                    var product = new Product
                    {
                        Id = Guid.NewGuid(),
                        CatalogId = catalogId,
                        CreatedDate = DateTime.UtcNow,
                        Name = GetCellValue(row, 0, formatter),
                        Code = GetCellValue(row, 1, formatter),
                        Category = GetCellValue(row, 2, formatter),
                        Price = ParseDecimal(GetCellValue(row, 3, formatter)) ?? 0,
                        StockQuantity = ParseInt(GetCellValue(row, 4, formatter)) ?? 0,
                        Description = GetCellValue(row, 5, formatter)
                    };

                    if (!string.IsNullOrWhiteSpace(product.Name) && !string.IsNullOrWhiteSpace(product.Code))
                    {
                        products.Add(product);
                    }
                }
                catch
                {
                    continue;
                }
            }

            return products;
        }

        public List<StockImportRow> ParseStockRows(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return extension == ".csv" ? ParseStockRowsFromCsv(file) : ParseStockRowsFromExcel(file);
        }

        private List<StockImportRow> ParseStockRowsFromExcel(IFormFile file)
        {
            var rows = new List<StockImportRow>();
            var formatter = new DataFormatter(CultureInfo.InvariantCulture);

            using var stream = file.OpenReadStream();
            stream.Position = 0;

            var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0);
            var headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow == null) return rows;

            var headerMap = BuildHeaderMap(headerRow, formatter);
            var codeIndex = FindHeaderIndex(headerMap, "code", "kod", "parcakodu", "urunkodu", "stokkodu");
            var stockIndex = FindHeaderIndex(headerMap, "stock", "stok", "stockquantity", "qty", "quantity", "miktar", "adet");
            var priceIndex = FindHeaderIndex(headerMap, "price", "fiyat", "unitprice", "birimfiyat");
            var nameIndex = FindHeaderIndex(headerMap, "name", "ad", "urunadi", "parcaadi");
            var categoryIndex = FindHeaderIndex(headerMap, "category", "kategori");
            var descriptionIndex = FindHeaderIndex(headerMap, "description", "aciklama", "desc");

            if (codeIndex == -1 || stockIndex == -1)
            {
                throw new InvalidOperationException("Dosyada zorunlu başlıklar bulunamadı. Gerekli alanlar: code/kod ve stock/stok.");
            }

            for (int i = sheet.FirstRowNum + 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var code = GetCellValue(row, codeIndex, formatter).Trim();
                if (string.IsNullOrWhiteSpace(code)) continue;

                var stockText = GetCellValue(row, stockIndex, formatter);
                rows.Add(new StockImportRow
                {
                    RowNumber = i + 1,
                    Code = code,
                    StockQuantity = ParseInt(stockText),
                    Price = priceIndex >= 0 ? ParseDecimal(GetCellValue(row, priceIndex, formatter)) : null,
                    Name = nameIndex >= 0 ? NullIfEmpty(GetCellValue(row, nameIndex, formatter)) : null,
                    Category = categoryIndex >= 0 ? NullIfEmpty(GetCellValue(row, categoryIndex, formatter)) : null,
                    Description = descriptionIndex >= 0 ? NullIfEmpty(GetCellValue(row, descriptionIndex, formatter)) : null
                });
            }

            return rows;
        }

        private List<StockImportRow> ParseStockRowsFromCsv(IFormFile file)
        {
            var rows = new List<StockImportRow>();

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine)) return rows;

            var delimiter = DetectDelimiter(headerLine);
            var headers = ParseCsvLine(headerLine, delimiter)
                .Select(NormalizeHeader)
                .ToList();

            var codeIndex = FindHeaderIndex(headers, "code", "kod", "parcakodu", "urunkodu", "stokkodu");
            var stockIndex = FindHeaderIndex(headers, "stock", "stok", "stockquantity", "qty", "quantity", "miktar", "adet");
            var priceIndex = FindHeaderIndex(headers, "price", "fiyat", "unitprice", "birimfiyat");
            var nameIndex = FindHeaderIndex(headers, "name", "ad", "urunadi", "parcaadi");
            var categoryIndex = FindHeaderIndex(headers, "category", "kategori");
            var descriptionIndex = FindHeaderIndex(headers, "description", "aciklama", "desc");

            if (codeIndex == -1 || stockIndex == -1)
            {
                throw new InvalidOperationException("Dosyada zorunlu başlıklar bulunamadı. Gerekli alanlar: code/kod ve stock/stok.");
            }

            var rowNumber = 1;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line, delimiter);
                var code = GetCsvValue(values, codeIndex).Trim();
                if (string.IsNullOrWhiteSpace(code)) continue;

                var stockText = GetCsvValue(values, stockIndex);
                rows.Add(new StockImportRow
                {
                    RowNumber = rowNumber,
                    Code = code,
                    StockQuantity = ParseInt(stockText),
                    Price = priceIndex >= 0 ? ParseDecimal(GetCsvValue(values, priceIndex)) : null,
                    Name = nameIndex >= 0 ? NullIfEmpty(GetCsvValue(values, nameIndex)) : null,
                    Category = categoryIndex >= 0 ? NullIfEmpty(GetCsvValue(values, categoryIndex)) : null,
                    Description = descriptionIndex >= 0 ? NullIfEmpty(GetCsvValue(values, descriptionIndex)) : null
                });
            }

            return rows;
        }

        private static Dictionary<string, int> BuildHeaderMap(IRow row, DataFormatter formatter)
        {
            var map = new Dictionary<string, int>();
            for (int i = row.FirstCellNum; i < row.LastCellNum; i++)
            {
                if (i < 0) continue;
                var value = formatter.FormatCellValue(row.GetCell(i));
                var normalized = NormalizeHeader(value);
                if (!string.IsNullOrWhiteSpace(normalized) && !map.ContainsKey(normalized))
                {
                    map[normalized] = i;
                }
            }

            return map;
        }

        private static int FindHeaderIndex(Dictionary<string, int> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(NormalizeHeader(key), out var index)) return index;
            }

            return -1;
        }

        private static int FindHeaderIndex(List<string> headers, params string[] keys)
        {
            var normalizedKeys = keys.Select(NormalizeHeader).ToHashSet();
            for (int i = 0; i < headers.Count; i++)
            {
                if (normalizedKeys.Contains(headers[i])) return i;
            }

            return -1;
        }

        private static string NormalizeHeader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var lowered = value.Trim().ToLowerInvariant()
                .Replace('ı', 'i')
                .Replace('ş', 's')
                .Replace('ğ', 'g')
                .Replace('ü', 'u')
                .Replace('ö', 'o')
                .Replace('ç', 'c');

            var chars = lowered.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }

        private static int? ParseInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var cleaned = value.Trim().Replace(" ", string.Empty);
            if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)) return result;
            if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.GetCultureInfo("tr-TR"), out result)) return result;

            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue) && decimalValue % 1 == 0)
                return (int)decimalValue;
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out decimalValue) && decimalValue % 1 == 0)
                return (int)decimalValue;

            return null;
        }

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var cleaned = value.Trim().Replace(" ", string.Empty);
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)) return result;
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out result)) return result;

            return null;
        }

        private static string GetCellValue(IRow row, int cellIndex, DataFormatter formatter)
        {
            var cell = row.GetCell(cellIndex);
            return formatter.FormatCellValue(cell) ?? string.Empty;
        }

        private static string NullIfEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static char DetectDelimiter(string headerLine)
        {
            var comma = headerLine.Count(c => c == ',');
            var semicolon = headerLine.Count(c => c == ';');
            var tab = headerLine.Count(c => c == '\t');

            if (tab >= comma && tab >= semicolon) return '\t';
            return semicolon > comma ? ';' : ',';
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

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

        private static string GetCsvValue(List<string> values, int index)
        {
            return index >= 0 && index < values.Count ? values[index] : string.Empty;
        }
    }
}
