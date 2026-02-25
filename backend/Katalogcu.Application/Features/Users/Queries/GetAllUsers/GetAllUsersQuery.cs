using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Users.Queries.GetAllUsers;

public sealed record GetAllUsersQuery : IRequest<OperationResult<IReadOnlyList<AppUser>>>;
