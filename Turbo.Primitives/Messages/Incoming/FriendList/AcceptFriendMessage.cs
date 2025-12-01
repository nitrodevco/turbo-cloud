using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record AcceptFriendMessage : IMessageEvent
{
    public required List<int> Friends { get; init; }
}
