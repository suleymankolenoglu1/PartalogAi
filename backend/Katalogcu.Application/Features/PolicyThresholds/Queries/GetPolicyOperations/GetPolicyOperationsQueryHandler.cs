using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyOperations;

public sealed class GetPolicyOperationsQueryHandler
    : IRequestHandler<GetPolicyOperationsQuery, OperationResult<IReadOnlyList<PolicyThresholdOperationDto>>>
{
    private readonly IPolicyThresholdRepository _repository;

    public GetPolicyOperationsQueryHandler(IPolicyThresholdRepository repository)
    {
        _repository = repository;
    }

    public async Task<OperationResult<IReadOnlyList<PolicyThresholdOperationDto>>> Handle(
        GetPolicyOperationsQuery request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 100);
        var logs = await _repository.GetRecentPolicyOperationLogsAsync(
            Math.Max(take * 4, 40),
            cancellationToken);

        HashSet<string>? ownedCatalogIds = null;
        if (!request.Actor.IsPlatformAdmin)
        {
            var scopeKeys = await _repository.GetOwnedCatalogScopeKeysAsync(request.Actor.UserId, cancellationToken);
            ownedCatalogIds = scopeKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var operations = new List<PolicyThresholdOperationDto>();
        foreach (var log in logs)
        {
            if (!PolicyThresholdOperationParser.TryParseJsonObject(log.Payload, out var payload))
            {
                continue;
            }

            var scope = PolicyThresholdOperationParser.ExtractAuditScope(payload);
            if (!CanViewPolicyOperation(request.Actor, scope, ownedCatalogIds))
            {
                continue;
            }

            operations.Add(new PolicyThresholdOperationDto
            {
                Id = log.Id,
                Action = log.Action,
                Title = PolicyThresholdOperationParser.MapPolicyOperationTitle(log.Action),
                ActorEmail = log.ActorEmail,
                ActorRole = log.ActorRole,
                CreatedAt = log.CreatedDate,
                ScopeType = scope.ScopeType,
                ScopeKey = scope.ScopeKey,
                ScopeLabel = PolicyThresholdOperationParser.BuildPolicyScopeLabel(scope),
                EvaluationCaseCount = PolicyThresholdOperationParser.ExtractInt(payload, "after", "evaluation", "caseCount"),
                PromotedCaseCount = PolicyThresholdOperationParser.ExtractInt(payload, "after", "appended"),
                SkippedCaseCount = PolicyThresholdOperationParser.ExtractInt(payload, "after", "skipped"),
                Note = PolicyThresholdOperationParser.ExtractString(payload, "after", "note")
                       ?? PolicyThresholdOperationParser.ExtractString(payload, "after", "policy", "Notes")
                       ?? PolicyThresholdOperationParser.ExtractString(payload, "after", "active", "Notes")
            });

            if (operations.Count >= take)
            {
                break;
            }
        }

        return OperationResult<IReadOnlyList<PolicyThresholdOperationDto>>.Success(operations);
    }

    private static bool CanViewPolicyOperation(
        PolicyThresholdActor actor,
        PolicyAuditScope scope,
        HashSet<string>? ownedCatalogIds)
    {
        if (actor.IsPlatformAdmin)
        {
            return true;
        }

        return string.Equals(scope.ScopeType, PolicyThreshold.CatalogScope, StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(scope.ScopeKey)
               && ownedCatalogIds is not null
               && ownedCatalogIds.Contains(scope.ScopeKey);
    }
}
