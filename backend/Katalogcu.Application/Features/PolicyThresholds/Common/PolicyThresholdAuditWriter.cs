using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.PolicyThresholds.Common;

public sealed class PolicyThresholdAuditWriter : IPolicyThresholdAuditWriter
{
    private readonly IPolicyThresholdRepository _repository;

    public PolicyThresholdAuditWriter(IPolicyThresholdRepository repository)
    {
        _repository = repository;
    }

    public void AddAuditLog(
        PolicyThresholdActor actor,
        string action,
        string? scopeType,
        object? before,
        object? after)
    {
        _repository.AddAuditLog(new PlatformAuditLog
        {
            ActorUserId = actor.UserId == Guid.Empty ? null : actor.UserId,
            TargetOwnerUserId = null,
            Action = action,
            ActorEmail = actor.Email,
            ActorRole = actor.Role,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            Payload = JsonSerializer.Serialize(new
            {
                scopeType,
                before,
                after
            }),
            CreatedDate = DateTime.UtcNow
        });
    }
}
