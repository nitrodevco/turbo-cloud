using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

/// <summary>One fragment of a wired chest's furni contents; a large chest is sent in several.</summary>
[GenerateSerializer, Immutable]
public sealed record ItemsChestContentsChunkMessageComposer : IComposer
{
    [Id(0)]
    public required int ChestId { get; init; }

    [Id(1)]
    public required int TotalFragments { get; init; }

    [Id(2)]
    public required int FragmentNo { get; init; }

    [Id(3)]
    public required ImmutableArray<ChestStorageSnapshot> Items { get; init; }
}
