using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record FollowFriendMessage : IMessageEvent
{
    public required PlayerId PlayerId { get; init; }
}
