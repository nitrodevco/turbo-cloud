using System.Collections.Generic;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

internal sealed class RoomLiveState
{
    public RoomSnapshot RoomSnapshot { get; internal set; } = default!;

    public Dictionary<long, IRoomFloorItem> FloorItemsById { get; } = [];
    public Dictionary<long, IRoomWallItem> WallItemsById { get; } = [];
    public bool IsFurniLoaded { get; internal set; } = false;

    public RoomModelSnapshot? Model { get; internal set; } = null;
    public double[] TileHeights { get; internal set; } = [];
    public short[] TileEncodedHeights { get; internal set; } = [];
    public RoomTileFlags[] TileFlags { get; internal set; } = [];
    public long[] TileHighestFloorItems { get; internal set; } = [];
    public List<long>[] TileFloorStacks { get; internal set; } = [];
    public bool IsMapBuilt { get; internal set; } = false;
    public bool NeedsCompile { get; internal set; } = false;
    public HashSet<int> DirtyTileIds { get; } = [];
    public HashSet<long> DirtyItemIds { get; } = [];

    public HashSet<long> PlayerIdsWithRights { get; } = [];
}
