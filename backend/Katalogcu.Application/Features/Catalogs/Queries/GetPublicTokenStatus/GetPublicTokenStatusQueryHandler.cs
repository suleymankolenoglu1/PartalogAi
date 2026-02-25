using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetPublicTokenStatus;

public sealed class GetPublicTokenStatusQueryHandler : IRequestHandler<GetPublicTokenStatusQuery, OperationResult<PublicTokenStatusDto>>
{
    private readonly IUserRepository _userRepository;

    public GetPublicTokenStatusQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<OperationResult<PublicTokenStatusDto>> Handle(GetPublicTokenStatusQuery request, CancellationToken cancellationToken)
    {
        var userState = await _userRepository.GetPublicLinkStateAsync(request.UserId, cancellationToken);
        if (userState == null)
        {
            return OperationResult<PublicTokenStatusDto>.Failure("unauthorized", "Unauthorized");
        }

        return OperationResult<PublicTokenStatusDto>.Success(new PublicTokenStatusDto
        {
            Enabled = userState.PublicLinkEnabled,
            Version = userState.PublicLinkVersion
        });
    }
}
