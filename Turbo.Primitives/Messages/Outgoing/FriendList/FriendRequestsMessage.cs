using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record FriendRequestsMessage : IComposer
{
    public required List<FriendRequestSnapshot> Requests { get; init; }
}
