using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record RemoveFriendMessage : IMessageEvent
{
    public required List<int> FriendIds { get; init; }
}
