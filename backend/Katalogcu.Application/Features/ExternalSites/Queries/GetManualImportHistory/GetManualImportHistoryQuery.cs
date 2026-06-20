using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetManualImportHistory;

public sealed record GetManualImportHistoryQuery(Guid SiteId) : IRequest<OperationResult<ManualImportHistoryResponse>>;
