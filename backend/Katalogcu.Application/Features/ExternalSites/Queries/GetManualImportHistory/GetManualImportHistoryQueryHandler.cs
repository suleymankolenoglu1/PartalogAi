using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetManualImportHistory;

public sealed class GetManualImportHistoryQueryHandler
    : IRequestHandler<GetManualImportHistoryQuery, OperationResult<ManualImportHistoryResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IManualImportFileRepository _manualImportFileRepository;

    public GetManualImportHistoryQueryHandler(
        ICurrentUserService currentUser,
        IManualImportFileRepository manualImportFileRepository)
    {
        _currentUser = currentUser;
        _manualImportFileRepository = manualImportFileRepository;
    }

    public async Task<OperationResult<ManualImportHistoryResponse>> Handle(
        GetManualImportHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ManualImportHistoryResponse>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        var items = await _manualImportFileRepository.GetHistoryBySiteAsync(request.SiteId, _currentUser.UserId, cancellationToken);
        return OperationResult<ManualImportHistoryResponse>.Success(new ManualImportHistoryResponse
        {
            SiteId = request.SiteId,
            Items = items.Select(x => new ManualImportHistoryItemDto
            {
                Id = x.Id,
                FileName = x.FileName,
                FileType = x.FileType,
                RowCount = x.RowCount,
                Status = x.Status,
                ErrorSummary = x.ErrorSummary,
                ImportedAtUtc = x.ImportedAtUtc
            }).ToList()
        });
    }
}
