using System.Text;
using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Katalogcu.Infrastructure.Services;

public sealed class PolicyRegressionCaseFileStore : IPolicyRegressionCaseStore
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public PolicyRegressionCaseFileStore(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<PolicyRegressionCaseStoreResult> PromoteAsync(
        IReadOnlyCollection<PolicyRegressionCaseDraft> cases,
        string? note,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        var targetPath = ResolveFeedbackRegressionCasesPath();
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var appended = new List<PolicyRegressionCaseDraft>();
        var skipped = new List<PolicyRegressionCaseDraft>();

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            var existingKeys = LoadRegressionCaseKeys(targetPath);
            foreach (var evalCase in cases)
            {
                var key = string.IsNullOrWhiteSpace(evalCase.Id) ? evalCase.CanonicalJson : evalCase.Id;
                if (existingKeys.Contains(key))
                {
                    skipped.Add(evalCase);
                    continue;
                }

                existingKeys.Add(key);
                appended.Add(evalCase);
            }

            if (appended.Count > 0)
            {
                var header = $"# Promoted from PolicyThreshold admin at {DateTime.UtcNow:O} by {actorEmail}";
                if (!string.IsNullOrWhiteSpace(note))
                {
                    header += $" | {note}";
                }

                var builder = new StringBuilder();
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine(header);
                foreach (var evalCase in appended)
                {
                    builder.AppendLine(evalCase.CanonicalJson);
                }

                await File.AppendAllTextAsync(targetPath, builder.ToString(), Encoding.UTF8, cancellationToken);
            }
        }
        finally
        {
            FileLock.Release();
        }

        return new PolicyRegressionCaseStoreResult
        {
            Appended = appended.Count,
            Skipped = skipped.Count,
            Path = targetPath,
            AppendedCaseIds = appended
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList()
        };
    }

    public Task<PolicyRegressionCasePreviewResultDto> GetPreviewAsync(
        int take,
        CancellationToken cancellationToken)
    {
        var targetPath = ResolveFeedbackRegressionCasesPath();
        if (!File.Exists(targetPath))
        {
            return Task.FromResult(new PolicyRegressionCasePreviewResultDto
            {
                Items = [],
                Total = 0,
                Path = targetPath
            });
        }

        var rows = new List<PolicyRegressionCasePreviewItemDto>();
        var total = 0;
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(targetPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                total++;
                PolicyRegressionCaseParser.TryGetJsonString(root, "id", out var id);
                PolicyRegressionCaseParser.TryGetJsonString(root, "text", out var text);
                PolicyRegressionCaseParser.TryGetJsonString(root, "message", out var message);
                PolicyRegressionCaseParser.TryGetJsonString(root, "feedback_id", out var feedbackId);
                PolicyRegressionCaseParser.TryGetJsonString(root, "feedback_reason", out var feedbackReason);

                rows.Add(new PolicyRegressionCasePreviewItemDto
                {
                    LineNumber = lineNumber,
                    Id = NormalizeOptional(id, 256),
                    Text = NormalizeOptional(text, 400),
                    Message = NormalizeOptional(message, 400),
                    FeedbackId = NormalizeOptional(feedbackId, 256),
                    FeedbackReason = NormalizeOptional(feedbackReason, 240),
                    CatalogIds = PolicyRegressionCaseParser.GetJsonStringArray(root, "catalog_ids", "catalogIds"),
                    ExpectedCodes = PolicyRegressionCaseParser.GetJsonStringArray(root, "expected_codes", "expectedCodes"),
                    RequiredTerms = PolicyRegressionCaseParser.GetJsonStringArray(root, "required_terms", "requiredTerms"),
                    ForbiddenTerms = PolicyRegressionCaseParser.GetJsonStringArray(root, "forbidden_terms", "forbiddenTerms"),
                    ExpectNoCodes = PolicyRegressionCaseParser.GetJsonBool(root, "expect_no_codes", "expectNoCodes"),
                    HasContext = PolicyRegressionCaseParser.GetJsonProperty(root, "context_json", "contextJson", "context") is not null
                });
            }
            catch
            {
                // Hand-edited JSONL can contain malformed drafts; keep the preview resilient.
            }
        }

        var items = rows
            .AsEnumerable()
            .Reverse()
            .Take(take)
            .ToList();

        return Task.FromResult(new PolicyRegressionCasePreviewResultDto
        {
            Items = items,
            Total = total,
            Path = targetPath
        });
    }

    private static HashSet<string> LoadRegressionCaseKeys(string targetPath)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(targetPath))
        {
            return keys;
        }

        foreach (var rawLine in File.ReadLines(targetPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (PolicyRegressionCaseParser.TryGetJsonString(root, "id", out var id) && !string.IsNullOrWhiteSpace(id))
                {
                    keys.Add(id.Trim());
                }
                else
                {
                    keys.Add(JsonSerializer.Serialize(root));
                }
            }
            catch
            {
                // Keep the promotion path tolerant of hand-edited JSONL files.
            }
        }

        return keys;
    }

    private string ResolveFeedbackRegressionCasesPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("PARTALOG_FEEDBACK_REGRESSION_CASES_PATH")
                             ?? _configuration["ChatEval:FeedbackRegressionCasesPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(_environment.ContentRootPath, configuredPath));
        }

        var cursor = new DirectoryInfo(_environment.ContentRootPath);
        for (var i = 0; i < 6 && cursor is not null; i++)
        {
            var candidate = Path.Combine(cursor.FullName, "partalog-ai", "eval", "queries.feedback_regressions.jsonl");
            if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)))
            {
                return candidate;
            }

            cursor = cursor.Parent;
        }

        return Path.Combine(_environment.ContentRootPath, "App_Data", "eval", "queries.feedback_regressions.jsonl");
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return normalized;
    }
}
