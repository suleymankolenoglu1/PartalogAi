using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Customers.Common;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Customers.Commands.CreatePublicCustomerMachine;

public sealed class CreatePublicCustomerMachineCommandHandler
    : IRequestHandler<CreatePublicCustomerMachineCommand, OperationResult<PublicCustomerMachineDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public CreatePublicCustomerMachineCommandHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<OperationResult<PublicCustomerMachineDto>> Handle(
        CreatePublicCustomerMachineCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPublicSessionAsync(
            request.OwnerUserId,
            request.SessionToken,
            DateTime.UtcNow,
            cancellationToken);

        if (customer == null)
        {
            return OperationResult<PublicCustomerMachineDto>.Failure("unauthorized", "Oturum geçersiz.");
        }

        var machines = await _customerRepository.GetMachinesByCustomerIdAsync(customer.Id, cancellationToken);
        var makeActive = request.MakeActive || machines.Count == 0;
        if (makeActive)
        {
            foreach (var machine in machines.Where(m => m.IsActive))
            {
                machine.IsActive = false;
                machine.UpdatedDate = DateTime.UtcNow;
            }
        }

        var brand = request.Brand.Trim();
        var model = request.Model.Trim();
        var variant = NormalizeOptional(request.Variant);
        var displayName = NormalizeOptional(request.DisplayName)
            ?? string.Join(' ', new[] { brand, model, variant }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var entity = new CustomerMachine
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Brand = brand,
            Model = model,
            Variant = variant,
            MachineGroup = NormalizeOptional(request.MachineGroup),
            SerialNumber = NormalizeOptional(request.SerialNumber),
            DisplayName = displayName,
            IsActive = makeActive,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        await _customerRepository.AddMachineAsync(entity, cancellationToken);
        await _customerRepository.SaveChangesAsync(cancellationToken);
        return OperationResult<PublicCustomerMachineDto>.Success(CustomerMachineMapper.ToDto(entity));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
