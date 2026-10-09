using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

public record UpdateThreadMessage : IMessageEvent
{
    public required int GroupId { get; init; }

    public required int ThreadId { get; init; }

    public required bool IsSticky { get; init; }

    public required bool IsLocked { get; init; }
}
