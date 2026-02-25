using Katalogcu.Application.Features.Chat.Common;

namespace Katalogcu.Application.Common.Interfaces;

public interface IChatFeedbackStore
{
    Task SaveAsync(ChatFeedbackEntry entry, CancellationToken cancellationToken);
}
