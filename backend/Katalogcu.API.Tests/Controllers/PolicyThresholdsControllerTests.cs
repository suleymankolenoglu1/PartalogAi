using Katalogcu.Application.Features.PolicyThresholds.Common;
using Xunit;

namespace Katalogcu.API.Tests.Controllers;

public sealed class PolicyThresholdsControllerTests
{
    [Fact]
    public void RegressionCaseHash_MatchesEvalDtoForEquivalentJsonl()
    {
        const string jsonl = """
        # comment
        {"id":"FB_001","text":"model plakasi kodu nedir","catalog_ids":["catalog-1"],"expected_codes":["4109410"],"required_terms":["4109410"],"forbidden_terms":["99999"],"expect_no_codes":false}
        """;

        var parsedCases = PolicyRegressionCaseParser.ParseDrafts(jsonl);
        var parsedEvalCases = parsedCases.Select(x => x.EvalCase).ToList();
        var directEvalCases = new[]
        {
            new PolicyThresholdEvalCaseDto
            {
                Id = "FB_001",
                Text = "model plakasi kodu nedir",
                CatalogIds = ["catalog-1"],
                ExpectedCodes = ["4109410"],
                RequiredTerms = ["4109410"],
                ForbiddenTerms = ["99999"],
                ExpectNoCodes = false
            }
        };

        Assert.Single(parsedEvalCases);
        Assert.Equal(ComputeCasesHash(directEvalCases), ComputeCasesHash(parsedEvalCases));
    }

    [Fact]
    public void RegressionCaseHash_ChangesWhenJsonlIsTampered()
    {
        var original = new[]
        {
            new PolicyThresholdEvalCaseDto
            {
                Id = "FB_001",
                Text = "model plakasi kodu nedir",
                CatalogIds = ["catalog-1"],
                ExpectedCodes = ["4109410"]
            }
        };
        var tampered = new[]
        {
            new PolicyThresholdEvalCaseDto
            {
                Id = "FB_001",
                Text = "baska parca kodu nedir",
                CatalogIds = ["catalog-1"],
                ExpectedCodes = ["4109410"]
            }
        };

        Assert.NotEqual(ComputeCasesHash(original), ComputeCasesHash(tampered));
    }

    private static string ComputeCasesHash(IEnumerable<PolicyThresholdEvalCaseDto> cases)
    {
        return PolicyRegressionCaseParser.ComputeEvalCasesHash(cases);
    }
}
