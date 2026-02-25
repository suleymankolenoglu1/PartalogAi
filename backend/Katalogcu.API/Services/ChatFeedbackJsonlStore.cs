using System.Text;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Chat.Common;

namespace Katalogcu.API.Services;

public sealed class ChatFeedbackJsonlStore : IChatFeedbackStore
{
    private readonly IWebHostEnvironment _env;

    public ChatFeedbackJsonlStore(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task SaveAsync(ChatFeedbackEntry entry, CancellationToken cancellationToken)
    {
        var feedbackDir = Path.Combine(_env.ContentRootPath, "App_Data", "chat-feedback");
        Directory.CreateDirectory(feedbackDir);
        var feedbackPath = Path.Combine(feedbackDir, "index.jsonl");

        var line = System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine;
        await File.AppendAllTextAsync(feedbackPath, line, Encoding.UTF8, cancellationToken);
    }
}
