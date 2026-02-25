using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.Application.Features.Ai.Commands.RunExpertChat;

public sealed record RunExpertChatCommand(
    string? Text,
    IFormFile? Image,
    string? History,
    IReadOnlyCollection<string>? CatalogIds)
    : IRequest<OperationResult<AiChatResponseDto>>;
