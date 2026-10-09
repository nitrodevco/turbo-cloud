using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

public record ClaimDailyTaskMessage : IMessageEvent
{
    public int TaskId { get; init; }
}
