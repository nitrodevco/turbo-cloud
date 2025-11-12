using System.Collections.Generic;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record FriendListUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required List<FriendCategorySnapshot> FriendCategories { get; init; }

    [Id(1)]
    public required List<FriendListUpdateSnapshot> Updates { get; init; }
}
