namespace Katalogcu.API.Services;

public sealed class CatalogAiProcessingOptions
{
    public const string SectionName = "CatalogAiProcessing";

    // Toplam çalıştırma sayısıdır. 4 => ilk deneme + 3 retry.
    public int MaxAttempts { get; set; } = 4;
    public int BaseRetryDelaySeconds { get; set; } = 15;
    public int HangfireWorkerCount { get; set; } = 1;

    public int GetNormalizedMaxAttempts()
    {
        return Math.Clamp(MaxAttempts, 1, 10);
    }

    public TimeSpan GetBaseRetryDelay()
    {
        return TimeSpan.FromSeconds(Math.Clamp(BaseRetryDelaySeconds, 1, 300));
    }

    public int GetNormalizedWorkerCount()
    {
        return Math.Clamp(HangfireWorkerCount, 1, 20);
    }

    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        var multiplier = Math.Pow(2, Math.Max(0, attemptNumber - 1));
        var delayMs = GetBaseRetryDelay().TotalMilliseconds * multiplier;
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, TimeSpan.FromMinutes(10).TotalMilliseconds));
    }
}
