using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Queries.GetMachineModels;

public sealed class GetMachineModelsQueryHandler
    : IRequestHandler<GetMachineModelsQuery, OperationResult<IReadOnlyList<MachineModelDto>>>
{
    private readonly ICompatibilityRepository _compatibilityRepository;

    public GetMachineModelsQueryHandler(ICompatibilityRepository compatibilityRepository)
    {
        _compatibilityRepository = compatibilityRepository;
    }

    public async Task<OperationResult<IReadOnlyList<MachineModelDto>>> Handle(
        GetMachineModelsQuery request,
        CancellationToken cancellationToken)
    {
        var models = await _compatibilityRepository.GetMachineModelsAsync(cancellationToken);
        return OperationResult<IReadOnlyList<MachineModelDto>>.Success(models.Select(CompatibilityMapper.ToDto).ToList());
    }
}
