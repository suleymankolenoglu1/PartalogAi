using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Commands.CreatePartCompatibilityRule;

public sealed class CreatePartCompatibilityRuleCommandHandler
    : IRequestHandler<CreatePartCompatibilityRuleCommand, OperationResult<PartCompatibilityRuleDto>>
{
    private readonly ICompatibilityRepository _compatibilityRepository;

    public CreatePartCompatibilityRuleCommandHandler(ICompatibilityRepository compatibilityRepository)
    {
        _compatibilityRepository = compatibilityRepository;
    }

    public async Task<OperationResult<PartCompatibilityRuleDto>> Handle(
        CreatePartCompatibilityRuleCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _compatibilityRepository.CatalogItemExistsAsync(request.CatalogItemId, cancellationToken))
        {
            return OperationResult<PartCompatibilityRuleDto>.Failure("not_found", "Katalog parçası bulunamadı.");
        }

        var machineModel = await _compatibilityRepository.GetMachineModelAsync(request.MachineModelId, cancellationToken);
        if (machineModel == null)
        {
            return OperationResult<PartCompatibilityRuleDto>.Failure("not_found", "Makine modeli bulunamadı.");
        }

        var rule = new PartCompatibilityRule
        {
            Id = Guid.NewGuid(),
            CatalogItemId = request.CatalogItemId,
            MachineModelId = request.MachineModelId,
            CompatibilityLevel = request.CompatibilityLevel.Trim(),
            SourceType = request.SourceType.Trim(),
            Confidence = request.Confidence,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            MachineModel = machineModel,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        await _compatibilityRepository.AddRuleAsync(rule, cancellationToken);
        await _compatibilityRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<PartCompatibilityRuleDto>.Success(CompatibilityMapper.ToDto(rule));
    }
}
