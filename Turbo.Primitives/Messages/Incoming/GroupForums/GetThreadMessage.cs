using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

public record GetThreadMessage : IMessageEvent
{
    public required int GroupId { get; init; }

    public required int ThreadId { get; init; }
}
