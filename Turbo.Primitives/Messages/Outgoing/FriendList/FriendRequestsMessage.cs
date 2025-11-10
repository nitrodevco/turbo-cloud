using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record FriendRequestsMessage : IComposer
{
    public required List<FriendRequestSnapshot> Requests { get; init; }
}
