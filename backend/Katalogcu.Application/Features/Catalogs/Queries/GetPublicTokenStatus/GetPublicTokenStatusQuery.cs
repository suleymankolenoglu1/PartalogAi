using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicTokenStatus;

public sealed record GetPublicTokenStatusQuery(Guid UserId) : IRequest<OperationResult<PublicTokenStatusDto>>;
