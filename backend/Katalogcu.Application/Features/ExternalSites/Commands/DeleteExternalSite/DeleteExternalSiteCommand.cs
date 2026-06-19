using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.DeleteExternalSite;

public sealed record DeleteExternalSiteCommand(Guid SiteId) : IRequest<OperationResult<bool>>;
