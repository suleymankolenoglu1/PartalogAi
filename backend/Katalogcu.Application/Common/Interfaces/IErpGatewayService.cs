using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IErpGatewayService
{
    Task<ErpProductAvailabilityResult?> GetProductAvailabilityAsync(
        ErpProductAvailabilityRequest request,
        CancellationToken cancellationToken);
}
