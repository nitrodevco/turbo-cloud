using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

[GenerateSerializer, Immutable]
public sealed record FurniListAddOrUpdateEventMessageComposer : IComposer
{
    [Id(0)]
    public required FurnitureItemSnapshot Item { get; init; }
}
