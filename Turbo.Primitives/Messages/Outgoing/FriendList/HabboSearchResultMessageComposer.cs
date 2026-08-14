using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record HabboSearchResultMessageComposer : IComposer
{
    [Id(0)]
    public required List<MessengerSearchResultSnapshot> Friends { get; init; }

    [Id(1)]
    public required List<MessengerSearchResultSnapshot> Others { get; init; }
}
