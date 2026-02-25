using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.RotatePublicToken;

public sealed class RotatePublicTokenCommandHandler : IRequestHandler<RotatePublicTokenCommand, OperationResult<RotatePublicTokenDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IPublicCatalogLinkService _publicCatalogLinkService;

    public RotatePublicTokenCommandHandler(
        IUserRepository userRepository,
        ICatalogRepository catalogRepository,
        IPublicCatalogLinkService publicCatalogLinkService)
    {
        _userRepository = userRepository;
        _catalogRepository = catalogRepository;
        _publicCatalogLinkService = publicCatalogLinkService;
    }

    public async Task<OperationResult<RotatePublicTokenDto>> Handle(RotatePublicTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return OperationResult<RotatePublicTokenDto>.Failure("unauthorized", "Unauthorized");
        }

        IReadOnlyList<Guid> allowedIds = [];
        if (request.RequestedCatalogIds.Count > 0)
        {
            allowedIds = await _catalogRepository.GetPublishedCatalogIdsByUserAsync(
                request.UserId,
                request.RequestedCatalogIds,
                cancellationToken);

            if (allowedIds.Count == 0)
            {
                return OperationResult<RotatePublicTokenDto>.Failure("validation", "Seçilen kataloglar yayınlanmamış veya size ait değil.");
            }
        }

        user.PublicLinkVersion += 1;
        user.PublicLinkEnabled = true;
        user.UpdatedDate = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync(cancellationToken);

        var token = _publicCatalogLinkService.CreateToken(
            request.UserId,
            user.PublicLinkVersion,
            allowedIds.Count > 0 ? allowedIds : null);

        return OperationResult<RotatePublicTokenDto>.Success(new RotatePublicTokenDto
        {
            Token = token,
            Enabled = user.PublicLinkEnabled,
            Version = user.PublicLinkVersion
        });
    }
}
