using System.Collections.Generic;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

internal sealed class RoomLiveState
{
    public required RoomId RoomId { get; internal init; }
    public RoomSnapshot RoomSnapshot { get; internal set; } = default!;

    public Dictionary<RoomObjectId, IRoomFloorItem> FloorItemsById { get; } = [];
    public Dictionary<RoomObjectId, IRoomWallItem> WallItemsById { get; } = [];
    public Dictionary<RoomObjectId, IRoomAvatar> AvatarsByObjectId { get; } = [];
    public Dictionary<PlayerId, RoomObjectId> AvatarsByPlayerId { get; } = [];
    public Dictionary<PlayerId, string> OwnerNamesById { get; } = [];

    public RoomModelSnapshot? Model { get; internal set; } = null;
    public double[] TileHeights { get; internal set; } = [];
    public short[] TileEncodedHeights { get; internal set; } = [];
    public RoomTileFlags[] TileFlags { get; internal set; } = [];
    public RoomObjectId[] TileHighestFloorItems { get; internal set; } = [];
    public HashSet<RoomObjectId>[] TileFloorStacks { get; internal set; } = [];
    public HashSet<RoomObjectId>[] TileAvatarStacks { get; internal set; } = [];

    public HashSet<PlayerId> PlayerIdsWithRights { get; } = [];

    public HashSet<int> DirtyTileIdxs { get; } = [];
    public HashSet<int> DirtyHeightTileIds { get; } = [];
    public HashSet<RoomObjectId> DirtyFloorItemIds { get; } = [];
    public HashSet<RoomObjectId> DirtyWallItemIds { get; } = [];

    public bool IsMapReady { get; internal set; } = false;
    public bool IsFurniLoaded { get; internal set; } = false;
    public bool IsTileComputationPaused { get; internal set; } = false;
}
