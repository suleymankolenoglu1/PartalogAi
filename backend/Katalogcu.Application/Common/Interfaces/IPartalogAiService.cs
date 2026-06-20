using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPartalogAiService
{
    Task<List<Hotspot>> DetectHotspotsAsync(IFormFile file, Guid pageId, bool throwOnFailure = false);
    Task<HotspotLabelReadResultDto> ReadHotspotLabelAsync(IFormFile file);
    Task<List<ProductItemDto>> ExtractTableAsync(byte[] fileBytes, int pageNumber, bool throwOnFailure = false);
    Task<PageAnalysisResult> AnalyzePageAsync(byte[] fileBytes);
    Task<AiChatResponseDto> GetExpertChatResponseAsync(AiChatRequestDto request);
    Task<VisualFeedbackResponseDto> SaveVisualFeedbackAsync(VisualFeedbackRequestDto request);
    Task TriggerTrainingAsync();
    Task<float[]?> GetEmbeddingAsync(string text);
}

public sealed class HotspotLabelReadResultDto
{
    public bool Success { get; init; }
    public string? Label { get; init; }
    public double Confidence { get; init; }
    public string Message { get; init; } = string.Empty;
}
