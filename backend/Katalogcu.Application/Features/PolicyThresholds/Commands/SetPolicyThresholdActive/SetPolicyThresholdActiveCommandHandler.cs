using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.PolicyThresholds.Commands.SetPolicyThresholdActive;

public sealed class SetPolicyThresholdActiveCommandHandler
    : IRequestHandler<SetPolicyThresholdActiveCommand, OperationResult<PolicyThresholdDto>>
{
    private readonly IPolicyThresholdRepository _repository;
    private readonly IPolicyThresholdAccessService _accessService;
    private readonly IPolicyThresholdAuditWriter _auditWriter;

    public SetPolicyThresholdActiveCommandHandler(
        IPolicyThresholdRepository repository,
        IPolicyThresholdAccessService accessService,
        IPolicyThresholdAuditWriter auditWriter)
    {
        _repository = repository;
        _accessService = accessService;
        _auditWriter = auditWriter;
    }

    public async Task<OperationResult<PolicyThresholdDto>> Handle(
        SetPolicyThresholdActiveCommand request,
        CancellationToken cancellationToken)
    {
        return await _repository.ExecuteInTransactionAsync(
            async ct => await SetActiveInsideTransactionAsync(request, ct),
            cancellationToken);
    }

    private async Task<OperationResult<PolicyThresholdDto>> SetActiveInsideTransactionAsync(
        SetPolicyThresholdActiveCommand request,
        CancellationToken cancellationToken)
    {
        var target = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (target is null)
        {
            return OperationResult<PolicyThresholdDto>.Failure("not_found", "Policy threshold bulunamadı.");
        }

        var access = await _accessService.ValidateScopeAccessAsync(target.ScopeType, target.ScopeKey, request.Actor, cancellationToken);
        if (!access.IsSuccess)
        {
            return OperationResult<PolicyThresholdDto>.Failure(access.ErrorCode!, access.ErrorMessage!);
        }

        if (!request.IsActive)
        {
            if (!target.IsActive)
            {
                return OperationResult<PolicyThresholdDto>.Success(PolicyThresholdMapper.ToDto(target));
            }

            var deactivatedBefore = PolicyThresholdMapper.ToAuditShape(target);
            target.IsActive = false;
            target.UpdatedDate = DateTime.UtcNow;
            target.UpdatedBy = request.Actor.Email;

            _auditWriter.AddAuditLog(request.Actor, "PolicyThreshold.Deactivated", null, deactivatedBefore, null);
            await _repository.SaveChangesAsync(cancellationToken);
            return OperationResult<PolicyThresholdDto>.Success(PolicyThresholdMapper.ToDto(target));
        }

        if (target.IsActive)
        {
            return OperationResult<PolicyThresholdDto>.Success(PolicyThresholdMapper.ToDto(target));
        }

        var now = DateTime.UtcNow;
        var active = await _repository.GetActiveAsync(target.ScopeType, target.ScopeKey, cancellationToken);
        var before = new
        {
            active = active is null ? null : PolicyThresholdMapper.ToAuditShape(active),
            target = PolicyThresholdMapper.ToAuditShape(target)
        };

        if (active is not null && active.Id != target.Id)
        {
            active.IsActive = false;
            active.UpdatedDate = now;
            active.UpdatedBy = request.Actor.Email;
        }

        target.IsActive = true;
        target.UpdatedDate = now;
        target.UpdatedBy = request.Actor.Email;

        _auditWriter.AddAuditLog(
            request.Actor,
            "PolicyThreshold.Activated",
            target.ScopeType,
            before,
            new { active = PolicyThresholdMapper.ToAuditShape(target) });

        await _repository.SaveChangesAsync(cancellationToken);
        return OperationResult<PolicyThresholdDto>.Success(PolicyThresholdMapper.ToDto(target));
    }
}
