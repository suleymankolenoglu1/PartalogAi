using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Commands.CreateMachineModel;

public sealed class CreateMachineModelCommandHandler
    : IRequestHandler<CreateMachineModelCommand, OperationResult<MachineModelDto>>
{
    private readonly ICompatibilityRepository _compatibilityRepository;

    public CreateMachineModelCommandHandler(ICompatibilityRepository compatibilityRepository)
    {
        _compatibilityRepository = compatibilityRepository;
    }

    public async Task<OperationResult<MachineModelDto>> Handle(
        CreateMachineModelCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new MachineModel
        {
            Id = Guid.NewGuid(),
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim(),
            Variant = NormalizeOptional(request.Variant),
            MachineGroup = NormalizeOptional(request.MachineGroup),
            AliasesJson = NormalizeOptional(request.AliasesJson),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        await _compatibilityRepository.AddMachineModelAsync(entity, cancellationToken);
        await _compatibilityRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<MachineModelDto>.Success(CompatibilityMapper.ToDto(entity));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
