using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Commands.CreateMachineModel;

public sealed record CreateMachineModelCommand(
    string Brand,
    string Model,
    string? Variant,
    string? MachineGroup,
    string? AliasesJson)
    : IRequest<OperationResult<MachineModelDto>>;
