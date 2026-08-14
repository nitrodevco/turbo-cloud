using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record DeclineFriendMessage : IMessageEvent
{
    public required bool DeclineAll { get; init; }
    public required List<PlayerId> Friends { get; init; }
}
