using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.MarkMissingExternalProductsInactive;

public sealed record MarkMissingExternalProductsInactiveCommand(
    Guid SiteId,
    IReadOnlyCollection<string> SeenSourceUrls) : IRequest<OperationResult<int>>;
