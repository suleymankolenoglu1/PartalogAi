using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Auth.Common;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.SelectPlan;

public sealed record SelectPlanCommand(int Plan)
    : IRequest<OperationResult<AuthUserDto>>;
