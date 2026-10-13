using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Labels and regions that were added or changed, then the furni that no longer have one (Flash
/// <c>RoomEngine.updateFurnitureLabelsAndRegions</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureLabelsAndRegionsUpdateMessageComposer : IComposer
{
    [Id(0)]
    public ImmutableArray<FurnitureLabelSnapshot> AddedOrUpdated { get; init; } = [];

    [Id(1)]
    public ImmutableArray<RoomObjectId> RemovedIds { get; init; } = [];
}
