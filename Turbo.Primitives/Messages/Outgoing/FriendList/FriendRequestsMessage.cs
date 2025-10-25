using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.Friendlist;

public record FriendRequestsMessage : IComposer
{
    public required List<FriendRequestSnapshot> Requests { get; init; }
}
