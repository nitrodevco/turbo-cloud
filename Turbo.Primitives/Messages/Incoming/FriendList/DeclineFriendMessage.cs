using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record DeclineFriendMessage : IMessageEvent
{
    public bool DeclineAll { get; init; }
    public List<int>? Friends { get; init; }
}
