using Katalogcu.Domain.Entities;
using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken);

    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PublicLinkStateDto?> GetPublicLinkStateAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(AppUser user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
