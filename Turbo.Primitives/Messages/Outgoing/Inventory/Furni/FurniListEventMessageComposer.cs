using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

[GenerateSerializer, Immutable]
public sealed record FurniListEventMessageComposer : IComposer
{
    [Id(0)]
    public required int TotalFragments { get; init; }

    [Id(1)]
    public required int CurrentFragment { get; init; }

    [Id(2)]
    public required ImmutableArray<FurnitureItemSnapshot> Items { get; init; }
}
