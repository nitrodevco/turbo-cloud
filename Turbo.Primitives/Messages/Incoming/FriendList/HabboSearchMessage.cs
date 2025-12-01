using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record HabboSearchMessage : IMessageEvent
{
    public required string SearchQuery { get; init; }
}
