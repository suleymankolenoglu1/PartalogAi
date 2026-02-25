using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Auth.Common;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Queries.GetMe;

public sealed record GetMeQuery : IRequest<OperationResult<AuthUserDto>>;
