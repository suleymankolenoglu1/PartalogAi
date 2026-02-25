using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Auth.Common;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.UpdateMe;

public sealed record UpdateMeCommand(
    string FirstName,
    string LastName,
    string? CompanyName,
    string? PhoneNumber)
    : IRequest<OperationResult<AuthUserDto>>;
