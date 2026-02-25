using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.RevokePublicToken;

public sealed record RevokePublicTokenCommand(Guid UserId) : IRequest<OperationResult<PublicTokenStatusDto>>;
