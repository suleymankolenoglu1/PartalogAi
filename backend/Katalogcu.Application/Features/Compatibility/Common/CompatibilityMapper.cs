using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Features.Compatibility.Common;

public static class CompatibilityMapper
{
    public static MachineModelDto ToDto(MachineModel model)
    {
        return new MachineModelDto
        {
            Id = model.Id,
            Brand = model.Brand,
            Model = model.Model,
            Variant = model.Variant,
            MachineGroup = model.MachineGroup,
            AliasesJson = model.AliasesJson
        };
    }

    public static PartCompatibilityRuleDto ToDto(PartCompatibilityRule rule)
    {
        var machine = rule.MachineModel;
        var machineLabel = string.Join(' ', new[]
        {
            machine?.Brand,
            machine?.Model,
            machine?.Variant
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new PartCompatibilityRuleDto
        {
            Id = rule.Id,
            CatalogItemId = rule.CatalogItemId,
            MachineModelId = rule.MachineModelId,
            MachineLabel = machineLabel,
            CompatibilityLevel = rule.CompatibilityLevel,
            SourceType = rule.SourceType,
            Confidence = rule.Confidence,
            Notes = rule.Notes
        };
    }
}
