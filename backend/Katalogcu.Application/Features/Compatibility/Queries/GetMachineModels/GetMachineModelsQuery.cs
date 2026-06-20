using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Queries.GetMachineModels;

public sealed record GetMachineModelsQuery
    : IRequest<OperationResult<IReadOnlyList<MachineModelDto>>>;
