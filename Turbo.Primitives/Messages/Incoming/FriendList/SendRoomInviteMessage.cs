using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record SendRoomInviteMessage : IMessageEvent
{
    public required string Message { get; init; }
    public required List<int> FriendIds { get; init; }
}
