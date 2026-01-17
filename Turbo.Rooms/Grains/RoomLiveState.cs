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

public sealed class RoomLiveState
{
    public required RoomId RoomId { get; init; }
    public RoomSnapshot RoomSnapshot { get; set; } = default!;

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

    public HashSet<int> DirtyHeightTileIds { get; set; } = [];
    public HashSet<RoomObjectId> DirtyFloorItemIds { get; set; } = [];
    public HashSet<RoomObjectId> DirtyWallItemIds { get; set; } = [];

    public long AllVariablesHash { get; internal set; } = 0;

    public bool IsMapReady { get; internal set; } = false;
    public bool IsFurniLoaded { get; internal set; } = false;
    public bool IsTileComputationPaused { get; internal set; } = false;

    public long EpochMs { get; set; } = 0;
    public long NextAvatarBoundaryMs { get; set; } = 0;
    public long NextRollerBoundaryMs { get; set; } = 0;
    public long NextWiredBoundaryMs { get; set; } = 0;
}
