using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record NewFriendRequestMessageComposer : IComposer
{
    [Id(0)]
    public required FriendRequestSnapshot Request { get; init; }
}
