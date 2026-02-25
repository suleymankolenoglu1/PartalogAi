using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<OperationResult<LoginResponse>>;
