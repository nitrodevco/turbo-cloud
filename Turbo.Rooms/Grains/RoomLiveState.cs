using System.Collections.Generic;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

internal sealed class RoomLiveState
{
    public required int RoomId { get; internal init; }
    public RoomSnapshot RoomSnapshot { get; internal set; } = default!;

    public Dictionary<long, IRoomFloorItem> FloorItemsById { get; } = [];
    public Dictionary<long, IRoomWallItem> WallItemsById { get; } = [];
    public Dictionary<long, string> OwnerNamesById { get; } = [];
    public Dictionary<int, IRoomAvatar> AvatarsByObjectId { get; } = [];
    public Dictionary<long, int> AvatarsByPlayerId { get; } = [];

    public RoomModelSnapshot? Model { get; internal set; } = null;
    public double[] TileHeights { get; internal set; } = [];
    public short[] TileEncodedHeights { get; internal set; } = [];
    public RoomTileFlags[] TileFlags { get; internal set; } = [];
    public long[] TileHighestFloorItems { get; internal set; } = [];
    public HashSet<long>[] TileFloorStacks { get; internal set; } = [];
    public HashSet<int>[] TileAvatarStacks { get; internal set; } = [];

    public HashSet<long> PlayerIdsWithRights { get; } = [];

    public HashSet<int> DirtyTileIdxs { get; } = [];
    public HashSet<int> DirtyHeightTileIds { get; } = [];
    public HashSet<long> DirtyFloorItemIds { get; } = [];
    public HashSet<long> DirtyWallItemIds { get; } = [];

    public bool IsMapReady { get; internal set; } = false;
    public bool IsFurniLoaded { get; internal set; } = false;
    public bool IsTileComputationPaused { get; internal set; } = false;
}
