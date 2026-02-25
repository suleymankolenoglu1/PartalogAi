using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Chat.Common;
using MediatR;

namespace Katalogcu.Application.Features.Chat.Commands.SaveVisualFeedback;

public sealed record SaveVisualFeedbackCommand(
    Guid UserId,
    byte[] ImageBytes,
    string FileName,
    string ContentType,
    string? PartName,
    string? PartCode,
    string? MachineBrand,
    string? MachineType,
    string? Note)
    : IRequest<OperationResult<VisualFeedbackResultDto>>;
