namespace Katalogcu.API.Services;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
    public int ChatTimeoutSeconds { get; set; } = 45;
    public int StreamTimeoutSeconds { get; set; } = 90;
    public int LongRunningTimeoutSeconds { get; set; } = 300;
    public bool EnableItemEmbeddings { get; set; } = true;
    public int EmbeddingTimeoutSeconds { get; set; } = 20;

    public TimeSpan GetChatTimeout() => TimeSpan.FromSeconds(Math.Clamp(ChatTimeoutSeconds, 5, 300));

    public TimeSpan GetStreamTimeout() => TimeSpan.FromSeconds(Math.Clamp(StreamTimeoutSeconds, 15, 600));

    public TimeSpan GetLongRunningTimeout() => TimeSpan.FromSeconds(Math.Clamp(LongRunningTimeoutSeconds, 30, 900));

    public TimeSpan GetEmbeddingTimeout() => TimeSpan.FromSeconds(Math.Clamp(EmbeddingTimeoutSeconds, 2, 120));
}
