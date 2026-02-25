using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Commands.RevokePublicToken;

public sealed class RevokePublicTokenCommandHandler : IRequestHandler<RevokePublicTokenCommand, OperationResult<PublicTokenStatusDto>>
{
    private readonly IUserRepository _userRepository;

    public RevokePublicTokenCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<OperationResult<PublicTokenStatusDto>> Handle(RevokePublicTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return OperationResult<PublicTokenStatusDto>.Failure("unauthorized", "Unauthorized");
        }

        user.PublicLinkVersion += 1;
        user.PublicLinkEnabled = false;
        user.UpdatedDate = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return OperationResult<PublicTokenStatusDto>.Success(new PublicTokenStatusDto
        {
            Enabled = user.PublicLinkEnabled,
            Version = user.PublicLinkVersion
        });
    }
}
