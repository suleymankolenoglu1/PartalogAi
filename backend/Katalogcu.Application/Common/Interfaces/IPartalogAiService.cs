using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IPartalogAiService
{
    Task<List<Hotspot>> DetectHotspotsAsync(UploadedFile file, Guid pageId, bool throwOnFailure = false);
    Task<HotspotLabelReadResultDto> ReadHotspotLabelAsync(UploadedFile file);
    Task<List<ProductItemDto>> ExtractTableAsync(byte[] fileBytes, int pageNumber, bool throwOnFailure = false);
    Task<PageAnalysisResult> AnalyzePageAsync(byte[] fileBytes);
    Task<AiChatResponseDto> GetExpertChatResponseAsync(AiChatRequestDto request);
    Task<VisualFeedbackResponseDto> SaveVisualFeedbackAsync(VisualFeedbackRequestDto request);
    Task TriggerTrainingAsync();
    Task<IReadOnlyList<string>> BuildSearchTextsAsync(IReadOnlyList<IngestionSearchTextRequest> rows, CancellationToken cancellationToken = default);
    Task<float[]?> GetEmbeddingAsync(string text);
}

public sealed record IngestionSearchTextRequest(
    string? PartName,
    string? MachineBrandModel,
    string? MachineBrand,
    string? MachineModel,
    string? MachineGroup,
    string? Category,
    string? Description,
    string? PartCode,
    string? RefNo,
    string? Dimensions,
    string? Mechanism);

public sealed class HotspotLabelReadResultDto
{
    public bool Success { get; init; }
    public string? Label { get; init; }
    public double Confidence { get; init; }
    public string Message { get; init; } = string.Empty;
}
