using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.RotatePublicToken;

public sealed record RotatePublicTokenCommand(Guid UserId, IReadOnlyCollection<Guid> RequestedCatalogIds)
    : IRequest<OperationResult<RotatePublicTokenDto>>;
