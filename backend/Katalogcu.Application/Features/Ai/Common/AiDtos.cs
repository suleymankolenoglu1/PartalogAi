using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.Application.Features.Ai.Common;

public sealed class AiChatRequestDto
{
    public string? Text { get; set; }
    public List<ChatMessageDto> History { get; set; } = [];
    public IFormFile? Image { get; set; }
    public List<string>? CatalogIds { get; set; }
    public string? UserPlan { get; set; }
    public int? AiLimitPerMonth { get; set; }
    public int? AiUsedThisMonth { get; set; }
    public string? ContextJson { get; set; }
    public string? PolicyThresholdOverride { get; set; }
}

public sealed class ChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = string.Empty;
}

public sealed class AiChatResponseDto
{
    [JsonPropertyName("answer")]
    public string? Answer { get; set; }

    [JsonPropertyName("sources")]
    public List<ChatSourceDto>? Sources { get; set; }

    [JsonPropertyName("debug_intent")]
    public object? DebugIntent { get; set; }
}

public sealed class ChatSourceDto
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("machine_model")]
    public string? Model { get; set; }

    [JsonPropertyName("model")]
    public string? LegacyModel { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("desc")]
    public string? LegacyDescription { get; set; }

    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("catalogId")]
    public Guid? CatalogId { get; set; }

    [JsonPropertyName("catalog_id")]
    public Guid? LegacyCatalogId { get; set; }

    [JsonPropertyName("pageNumber")]
    public string? PageNumber { get; set; }

    [JsonPropertyName("page_number")]
    public string? LegacyPageNumber { get; set; }

    [JsonPropertyName("similarity")]
    public double? Similarity { get; set; }
}

public sealed class PageAnalysisResult
{
    [JsonPropertyName("is_technical_drawing")]
    public bool IsTechnicalDrawing { get; set; }

    [JsonPropertyName("is_parts_list")]
    public bool IsPartsList { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "Başlıksız";
}

public sealed class ProductItemDto
{
    [JsonPropertyName("ref_number")]
    public string RefNumber { get; set; } = "0";

    [JsonPropertyName("part_code")]
    public string? PartCode { get; set; }

    [JsonPropertyName("part_name")]
    public string? PartName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("dimensions")]
    public string? Dimensions { get; set; }
}

public sealed class VisualFeedbackRequestDto
{
    public IFormFile? Image { get; set; }
    public string? PartName { get; set; }
    public string? PartCode { get; set; }
    public string? MachineBrand { get; set; }
    public string? MachineType { get; set; }
    public string? UserId { get; set; }
    public string? PublicToken { get; set; }
    public string? Note { get; set; }
}

public sealed class VisualFeedbackResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("record")]
    public object? Record { get; set; }
}
