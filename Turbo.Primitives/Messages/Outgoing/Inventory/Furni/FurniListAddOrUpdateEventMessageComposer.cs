using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

[GenerateSerializer, Immutable]
public sealed record FurniListAddOrUpdateEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<FurnitureItemSnapshot> Items { get; init; }
}
