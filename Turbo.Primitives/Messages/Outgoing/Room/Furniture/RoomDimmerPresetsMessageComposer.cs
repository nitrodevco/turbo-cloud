using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record RoomDimmerPresetsMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ItemId { get; init; }

    [Id(1)]
    public required bool IsOn { get; init; }

    [Id(2)]
    public required int SelectedPresetId { get; init; }

    [Id(3)]
    public required ImmutableArray<DimmerPresetSnapshot> Presets { get; init; }
}
