using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Auth.Common;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.CancelPlan;

public sealed record CancelPlanCommand : IRequest<OperationResult<AuthUserDto>>;
