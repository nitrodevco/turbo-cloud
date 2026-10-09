using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

public record PostMessageMessage : IMessageEvent
{
    public required int GroupId { get; init; }

    public required int ThreadId { get; init; }

    public required string Subject { get; init; }

    public required string Text { get; init; }
}
