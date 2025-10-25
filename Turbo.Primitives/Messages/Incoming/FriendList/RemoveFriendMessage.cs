using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record RemoveFriendMessage : IMessageEvent
{
    public required List<int> FriendIds { get; init; }
}
