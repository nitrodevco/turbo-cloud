using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record AcceptFriendResultMessageComposer : IComposer
{
    [Id(0)]
    public required List<AcceptFriendFailureSnapshot> Failures { get; init; }
}
