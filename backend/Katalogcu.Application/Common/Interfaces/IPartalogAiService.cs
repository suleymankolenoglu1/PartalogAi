using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPartalogAiService
{
    Task<List<Hotspot>> DetectHotspotsAsync(IFormFile file, Guid pageId);
    Task<List<ProductItemDto>> ExtractTableAsync(byte[] fileBytes, int pageNumber);
    Task<PageAnalysisResult> AnalyzePageAsync(byte[] fileBytes);
    Task<AiChatResponseDto> GetExpertChatResponseAsync(AiChatRequestDto request);
    Task<VisualFeedbackResponseDto> SaveVisualFeedbackAsync(VisualFeedbackRequestDto request);
    Task TriggerTrainingAsync();
    Task<float[]?> GetEmbeddingAsync(string text);
}
