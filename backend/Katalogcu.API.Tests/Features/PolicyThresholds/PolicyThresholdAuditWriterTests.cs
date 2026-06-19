using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Domain.Entities;
using Xunit;

namespace Katalogcu.API.Tests.Features.PolicyThresholds;

public sealed class PolicyThresholdAuditWriterTests
{
    [Fact]
    public void AddAuditLog_CopiesActorMetadataAndPayloadShape()
    {
        var repository = new FakePolicyThresholdRepository();
        var writer = new PolicyThresholdAuditWriter(repository);
        var actorId = Guid.NewGuid();
        var actor = new PolicyThresholdActor(
            actorId,
            true,
            "actor@example.com",
            "platformadmin",
            "127.0.0.1",
            "tests");

        writer.AddAuditLog(
            actor,
            "PolicyThreshold.Created",
            "Catalog",
            new { oldValue = 1 },
            new { newValue = 2 });

        var auditLog = Assert.Single(repository.AuditLogs);
        Assert.Equal(actorId, auditLog.ActorUserId);
        Assert.Null(auditLog.TargetOwnerUserId);
        Assert.Equal("PolicyThreshold.Created", auditLog.Action);
        Assert.Equal("actor@example.com", auditLog.ActorEmail);
        Assert.Equal("platformadmin", auditLog.ActorRole);
        Assert.Equal("127.0.0.1", auditLog.IpAddress);
        Assert.Equal("tests", auditLog.UserAgent);
        Assert.True(auditLog.CreatedDate > DateTime.MinValue);

        using var document = JsonDocument.Parse(auditLog.Payload!);
        var root = document.RootElement;
        Assert.Equal("Catalog", root.GetProperty("scopeType").GetString());
        Assert.Equal(1, root.GetProperty("before").GetProperty("oldValue").GetInt32());
        Assert.Equal(2, root.GetProperty("after").GetProperty("newValue").GetInt32());
    }

    [Fact]
    public void AddAuditLog_ConvertsEmptyActorUserIdToNull()
    {
        var repository = new FakePolicyThresholdRepository();
        var writer = new PolicyThresholdAuditWriter(repository);
        var actor = new PolicyThresholdActor(
            Guid.Empty,
            false,
            "actor@example.com",
            "admin",
            null,
            null);

        writer.AddAuditLog(actor, "PolicyThreshold.Updated", null, null, null);

        var auditLog = Assert.Single(repository.AuditLogs);
        Assert.Null(auditLog.ActorUserId);

        using var document = JsonDocument.Parse(auditLog.Payload!);
        Assert.True(document.RootElement.TryGetProperty("scopeType", out var scopeType));
        Assert.Equal(JsonValueKind.Null, scopeType.ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("before").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("after").ValueKind);
    }

    private sealed class FakePolicyThresholdRepository : IPolicyThresholdRepository
    {
        public List<PlatformAuditLog> AuditLogs { get; } = [];

        public Task<IReadOnlyList<PolicyThreshold>> GetPolicyThresholdsAsync(
            bool includeInactive,
            string? scopeType,
            Guid actorUserId,
            bool isPlatformAdmin,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PolicyThreshold?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PolicyThreshold?> GetActiveAsync(string scopeType, string scopeKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> UserOwnsCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetOwnedCatalogScopeKeysAsync(Guid userId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<PolicyThresholdOperationLog>> GetRecentPolicyOperationLogsAsync(
            int take,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void AddPolicyThreshold(PolicyThreshold threshold)
            => throw new NotSupportedException();

        public void AddAuditLog(PlatformAuditLog auditLog)
        {
            AuditLogs.Add(auditLog);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
