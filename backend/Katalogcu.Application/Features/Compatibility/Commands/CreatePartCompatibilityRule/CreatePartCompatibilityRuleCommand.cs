using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Commands.CreatePartCompatibilityRule;

public sealed record CreatePartCompatibilityRuleCommand(
    Guid CatalogItemId,
    Guid MachineModelId,
    string CompatibilityLevel,
    string SourceType,
    decimal Confidence,
    string? Notes)
    : IRequest<OperationResult<PartCompatibilityRuleDto>>;
