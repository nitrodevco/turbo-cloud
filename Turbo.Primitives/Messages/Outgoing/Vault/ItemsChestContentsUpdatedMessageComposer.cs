using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

/// <summary>The furni that left and entered a wired chest since the client's last view of it.</summary>
[GenerateSerializer, Immutable]
public sealed record ItemsChestContentsUpdatedMessageComposer : IComposer
{
    [Id(0)]
    public required int ChestId { get; init; }

    [Id(1)]
    public required ImmutableArray<int> RemovedItemIds { get; init; }

    [Id(2)]
    public required ImmutableArray<ChestStorageSnapshot> AddedItems { get; init; }
}
