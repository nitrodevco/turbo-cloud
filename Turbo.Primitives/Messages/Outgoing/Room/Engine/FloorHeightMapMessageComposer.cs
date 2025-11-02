using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

public record FloorHeightMapMessageComposer : IComposer
{
    public required RoomScaleType ScaleType { get; init; }
    public required int FixedWallsHeight { get; init; }
    public required string ModelData { get; init; }
    public required List<AreaHideDataSnapshot> AreaHideData { get; init; }
}
