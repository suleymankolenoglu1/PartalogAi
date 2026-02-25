using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.Register;

public sealed record RegisterCommand(string FullName, string Email, string Password) : IRequest<OperationResult<RegisterResponse>>;
