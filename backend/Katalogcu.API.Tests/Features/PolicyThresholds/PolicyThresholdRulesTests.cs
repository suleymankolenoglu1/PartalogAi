using Katalogcu.Application.Features.PolicyThresholds.Common;
using Xunit;

namespace Katalogcu.API.Tests.Features.PolicyThresholds;

public sealed class PolicyThresholdRulesTests
{
    [Fact]
    public void ValidateAndNormalize_NormalizesGlobalAndBrandScopes()
    {
        var global = PolicyThresholdRules.ValidateAndNormalize(new PolicyThresholdRequestDto
        {
            ScopeType = " global ",
            ScopeKey = "ignored",
            HighConfidence = 0.8m
        });
        var brand = PolicyThresholdRules.ValidateAndNormalize(new PolicyThresholdRequestDto
        {
            ScopeType = "BRAND",
            ScopeKey = "  Acme  ",
            LowConfidence = 0.2m
        });

        Assert.True(global.IsSuccess, global.ErrorMessage);
        Assert.Equal("Global", global.Value.ScopeType);
        Assert.Equal("default", global.Value.ScopeKey);
        Assert.True(brand.IsSuccess, brand.ErrorMessage);
        Assert.Equal("Brand", brand.Value.ScopeType);
        Assert.Equal("acme", brand.Value.ScopeKey);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsThresholdsOutsideZeroToOne()
    {
        var result = PolicyThresholdRules.ValidateAndNormalize(new PolicyThresholdRequestDto
        {
            ScopeType = "Catalog",
            ScopeKey = Guid.NewGuid().ToString(),
            HighConfidence = 1.2m
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("validation", result.ErrorCode);
        Assert.Equal("Threshold değerleri 0 ile 1 arasında olmalıdır.", result.ErrorMessage);
    }
}
