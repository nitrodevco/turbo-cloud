using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

public record GetThreadsMessage : IMessageEvent
{
    public required int GroupId { get; init; }

    public required int StartIndex { get; init; }

    public required int Amount { get; init; }
}
