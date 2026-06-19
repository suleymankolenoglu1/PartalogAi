using Katalogcu.API.Services;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class PolicyThresholdEvaluationTokenServiceTests
{
    [Fact]
    public void Token_CanBeAppliedToMatchingPolicyDraft()
    {
        var tempDir = Directory.CreateTempSubdirectory("katalogcu-token-tests-");
        try
        {
            var service = CreateService(tempDir.FullName);
            var actorUserId = Guid.NewGuid();
            var request = new PolicyThresholdRequestDto
            {
                ScopeType = "Catalog",
                ScopeKey = "catalog-1",
                HighConfidence = 0.82m,
                LowConfidence = 0.42m
            };

            request.EvaluationToken = service.CreateToken(
                request,
                "Catalog",
                "catalog-1",
                actorUserId,
                3,
                "hash");

            var ok = service.TryApplyPolicyEvaluationToken(
                request,
                "Catalog",
                "catalog-1",
                actorUserId,
                out var error);

            Assert.True(ok, error);
            Assert.True(request.EvaluationPassed);
            Assert.Equal(3, request.EvaluationCaseCount);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Token_IsRejectedWhenPolicyDraftChanges()
    {
        var tempDir = Directory.CreateTempSubdirectory("katalogcu-token-tests-");
        try
        {
            var service = CreateService(tempDir.FullName);
            var actorUserId = Guid.NewGuid();
            var request = new PolicyThresholdRequestDto
            {
                ScopeType = "Catalog",
                ScopeKey = "catalog-1",
                HighConfidence = 0.82m
            };

            request.EvaluationToken = service.CreateToken(
                request,
                "Catalog",
                "catalog-1",
                actorUserId,
                3,
                "hash");
            request.HighConfidence = 0.9m;

            var ok = service.TryApplyPolicyEvaluationToken(
                request,
                "Catalog",
                "catalog-1",
                actorUserId,
                out var error);

            Assert.False(ok);
            Assert.Equal("Eval token bu policy taslağıyla eşleşmiyor. Eval'i tekrar çalıştır.", error);
            Assert.False(request.EvaluationPassed);
            Assert.Equal(0, request.EvaluationCaseCount);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static PolicyThresholdEvaluationTokenService CreateService(string keyDirectory)
    {
        var provider = DataProtectionProvider.Create(new DirectoryInfo(keyDirectory));
        return new PolicyThresholdEvaluationTokenService(provider);
    }
}
