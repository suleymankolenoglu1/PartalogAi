using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicStorefront;

public sealed record GetPublicStorefrontQuery(Guid UserId) : IRequest<OperationResult<PublicStorefrontDto>>;
