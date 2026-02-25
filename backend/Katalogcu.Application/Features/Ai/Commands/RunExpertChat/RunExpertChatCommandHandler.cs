using System.Text.Json;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Katalogcu.Application.Features.Ai.Commands.RunExpertChat;

public sealed class RunExpertChatCommandHandler : IRequestHandler<RunExpertChatCommand, OperationResult<AiChatResponseDto>>
{
    private readonly IPartalogAiService _partalogAiService;
    private readonly ILogger<RunExpertChatCommandHandler> _logger;

    public RunExpertChatCommandHandler(
        IPartalogAiService partalogAiService,
        ILogger<RunExpertChatCommandHandler> logger)
    {
        _partalogAiService = partalogAiService;
        _logger = logger;
    }

    public async Task<OperationResult<AiChatResponseDto>> Handle(
        RunExpertChatCommand request,
        CancellationToken cancellationToken)
    {
        List<ChatMessageDto> chatHistory = [];
        if (!string.IsNullOrWhiteSpace(request.History))
        {
            try
            {
                chatHistory = JsonSerializer.Deserialize<List<ChatMessageDto>>(request.History) ?? [];
            }
            catch
            {
                _logger.LogWarning("History parse edilemedi, sohbet sıfırdan başlıyor.");
            }
        }

        var aiRequest = new AiChatRequestDto
        {
            Text = request.Text,
            Image = request.Image,
            History = chatHistory,
            CatalogIds = request.CatalogIds?.ToList()
        };

        var response = await _partalogAiService.GetExpertChatResponseAsync(aiRequest);
        return OperationResult<AiChatResponseDto>.Success(response);
    }
}
