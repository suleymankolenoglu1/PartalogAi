using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.UpsertPolicyThreshold;

public sealed class UpsertPolicyThresholdCommandHandler
    : IRequestHandler<UpsertPolicyThresholdCommand, OperationResult<PolicyThresholdDto>>
{
    private readonly IPolicyThresholdRepository _repository;
    private readonly IPolicyThresholdAccessService _accessService;
    private readonly IPolicyThresholdAuditWriter _auditWriter;

    public UpsertPolicyThresholdCommandHandler(
        IPolicyThresholdRepository repository,
        IPolicyThresholdAccessService accessService,
        IPolicyThresholdAuditWriter auditWriter)
    {
        _repository = repository;
        _accessService = accessService;
        _auditWriter = auditWriter;
    }

    public async Task<OperationResult<PolicyThresholdDto>> Handle(
        UpsertPolicyThresholdCommand request,
        CancellationToken cancellationToken)
    {
        var normalized = PolicyThresholdRules.ValidateAndNormalize(request.Request);
        if (!normalized.IsSuccess)
        {
            return OperationResult<PolicyThresholdDto>.Failure(normalized.ErrorCode!, normalized.ErrorMessage!);
        }

        var (scopeType, scopeKey) = normalized.Value;
        var access = await _accessService.ValidateScopeAccessAsync(scopeType, scopeKey, request.Actor, cancellationToken);
        if (!access.IsSuccess)
        {
            return OperationResult<PolicyThresholdDto>.Failure(access.ErrorCode!, access.ErrorMessage!);
        }

        return await _repository.ExecuteInTransactionAsync(
            async ct => await UpsertInsideTransactionAsync(request, scopeType, scopeKey, ct),
            cancellationToken);
    }

    private async Task<OperationResult<PolicyThresholdDto>> UpsertInsideTransactionAsync(
        UpsertPolicyThresholdCommand request,
        string scopeType,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        PolicyThreshold? current = null;
        if (request.Id.HasValue)
        {
            current = await _repository.GetByIdAsync(request.Id.Value, cancellationToken);
            if (current is null)
            {
                return OperationResult<PolicyThresholdDto>.Failure("not_found", "Policy threshold bulunamadı.");
            }

            if (!string.Equals(current.ScopeType, scopeType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(current.ScopeKey, scopeKey, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<PolicyThresholdDto>.Failure("validation", "Mevcut policy scope'u güncelleme sırasında değiştirilemez.");
            }
        }

        var active = await _repository.GetActiveAsync(scopeType, scopeKey, cancellationToken);
        var version = Math.Max(current?.Version ?? 0, active?.Version ?? 0) + 1;
        var now = DateTime.UtcNow;
        var before = active is null ? null : PolicyThresholdMapper.ToAuditShape(active);

        if (active is not null)
        {
            active.IsActive = false;
            active.UpdatedDate = now;
            active.UpdatedBy = request.Actor.Email;
        }

        if (current is not null && current != active && current.IsActive)
        {
            current.IsActive = false;
            current.UpdatedDate = now;
            current.UpdatedBy = request.Actor.Email;
        }

        var created = new PolicyThreshold
        {
            ScopeType = scopeType,
            ScopeKey = scopeKey,
            HighConfidence = request.Request.HighConfidence,
            LowConfidence = request.Request.LowConfidence,
            AmbiguityScoreDelta = request.Request.AmbiguityScoreDelta,
            Notes = PolicyThresholdRules.NormalizeOptional(request.Request.Notes, 1024),
            UpdatedBy = request.Actor.Email,
            IsActive = true,
            Version = version,
            CreatedDate = now,
            UpdatedDate = now
        };

        _repository.AddPolicyThreshold(created);
        _auditWriter.AddAuditLog(
            request.Actor,
            request.Id.HasValue ? "PolicyThreshold.Updated" : "PolicyThreshold.Created",
            scopeType,
            before,
            new
            {
                policy = PolicyThresholdMapper.ToAuditShape(created),
                evaluation = new
                {
                    required = request.Request.RequireEvaluation,
                    passed = request.Request.RequireEvaluation,
                    caseCount = request.Request.EvaluationCaseCount
                }
            });

        await _repository.SaveChangesAsync(cancellationToken);
        return OperationResult<PolicyThresholdDto>.Success(PolicyThresholdMapper.ToDto(created));
    }
}
