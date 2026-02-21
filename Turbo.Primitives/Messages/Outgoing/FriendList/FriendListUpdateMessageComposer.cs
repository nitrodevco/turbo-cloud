using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record FriendListUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required List<MessengerCategoryDto> Categories { get; init; }

    [Id(1)]
    public required List<MessengerUpdateSnapshot> Updates { get; init; }
}
