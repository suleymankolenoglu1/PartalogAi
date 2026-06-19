using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.ImportExternalSiteProductsFromFile;

public sealed class ImportExternalSiteProductsFromFileCommandHandler
    : IRequestHandler<ImportExternalSiteProductsFromFileCommand, OperationResult<ManualImportResultDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IManualImportService _manualImportService;

    public ImportExternalSiteProductsFromFileCommandHandler(
        ICurrentUserService currentUser,
        IManualImportService manualImportService)
    {
        _currentUser = currentUser;
        _manualImportService = manualImportService;
    }

    public async Task<OperationResult<ManualImportResultDto>> Handle(
        ImportExternalSiteProductsFromFileCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return OperationResult<ManualImportResultDto>.Failure("unauthorized", "Geçersiz kullanıcı.");
        }

        try
        {
            var normalizedFile = await NormalizeFileNameAsync(request.File, request.FileType, cancellationToken);
            var result = await _manualImportService.ImportFromFileAsync(
                normalizedFile,
                request.SiteId,
                _currentUser.UserId,
                cancellationToken);

            return OperationResult<ManualImportResultDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<ManualImportResultDto>.Failure("validation", ex.Message);
        }
    }

    private static async Task<UploadedFile> NormalizeFileNameAsync(UploadedFile file, string? fileType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileType))
        {
            return file;
        }

        var normalizedType = fileType.Trim().TrimStart('.').ToLowerInvariant();
        var currentExtension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (currentExtension == normalizedType)
        {
            return file;
        }

        var baseName = Path.GetFileNameWithoutExtension(file.FileName);
        var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);

        return UploadedFile.FromBytes(
            memoryStream.ToArray(),
            $"{baseName}.{normalizedType}",
            file.Name,
            file.ContentType);
    }
}
