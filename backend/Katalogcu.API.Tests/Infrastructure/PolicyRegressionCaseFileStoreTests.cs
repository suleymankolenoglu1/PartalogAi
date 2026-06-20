using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Katalogcu.API.Tests.Infrastructure;

public sealed class PolicyRegressionCaseFileStoreTests
{
    [Fact]
    public async Task PromoteAsync_AppendsToResolvedPathAndPreviewReturnsLatestCases()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "katalogcu-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var targetPath = Path.Combine(tempDir, "queries.feedback_regressions.jsonl");
        var previousPath = Environment.GetEnvironmentVariable("PARTALOG_FEEDBACK_REGRESSION_CASES_PATH");
        Environment.SetEnvironmentVariable("PARTALOG_FEEDBACK_REGRESSION_CASES_PATH", targetPath);

        try
        {
            await File.WriteAllTextAsync(
                targetPath,
                """
                # existing cases
                malformed-json
                {"id":"old-case","text":"old text","expected_codes":["OLD-001"]}

                """);

            var store = new PolicyRegressionCaseFileStore(
                new ConfigurationBuilder().Build(),
                new TestHostEnvironment(tempDir));
            var drafts = PolicyRegressionCaseParser.ParseDrafts(
                """
                {"id":"new-case","text":"new text","feedback_id":"fb-1","catalog_ids":["catalog-1"],"required_terms":["required"],"expect_no_codes":true}
                """);

            var promotion = await store.PromoteAsync(drafts, "note", "tester@example.com", CancellationToken.None);
            var preview = await store.GetPreviewAsync(10, CancellationToken.None);

            Assert.Equal(targetPath, promotion.Path);
            Assert.Equal(1, promotion.Appended);
            Assert.Equal(0, promotion.Skipped);
            Assert.Equal(new[] { "new-case" }, promotion.AppendedCaseIds);
            Assert.Equal(targetPath, preview.Path);
            Assert.Equal(2, preview.Total);
            Assert.Equal("new-case", preview.Items[0].Id);
            Assert.Equal("fb-1", preview.Items[0].FeedbackId);
            Assert.Equal(new[] { "catalog-1" }, preview.Items[0].CatalogIds);
            Assert.True(preview.Items[0].ExpectNoCodes);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PARTALOG_FEEDBACK_REGRESSION_CASES_PATH", previousPath);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Katalogcu.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
