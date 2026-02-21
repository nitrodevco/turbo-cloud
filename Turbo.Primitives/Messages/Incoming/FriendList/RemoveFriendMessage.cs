using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record RemoveFriendMessage : IMessageEvent
{
    public required List<PlayerId> FriendIds { get; init; }
}
