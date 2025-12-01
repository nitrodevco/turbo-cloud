using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record VisitUserMessage : IMessageEvent
{
    public required string PlayerName { get; init; }
}
