namespace Katalogcu.API.Contracts.Chat;

public sealed class AiChatRequestWithHistoryDto
{
    public string? Text { get; set; }
    public IFormFile? Image { get; set; }
    public string? History { get; set; }
    public string? PublicToken { get; set; }
}
