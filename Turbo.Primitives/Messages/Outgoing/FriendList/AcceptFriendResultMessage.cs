using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record AcceptFriendResultMessage : IComposer
{
    public required List<AcceptFriendFailureSnapshot> Failures { get; init; }
}
