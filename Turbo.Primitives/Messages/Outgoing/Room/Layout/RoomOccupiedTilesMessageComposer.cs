using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Messages.Outgoing.Room.Layout;

[GenerateSerializer, Immutable]
public sealed record RoomOccupiedTilesMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomTilePositionSnapshot> Tiles { get; init; }
}
