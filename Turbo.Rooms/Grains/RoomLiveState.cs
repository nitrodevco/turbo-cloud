using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains;

internal sealed class RoomLiveState
{
    public required RoomId RoomId { get; init; }
    public RoomSnapshot RoomSnapshot { get; set; } = default!;

    public Dictionary<RoomObjectId, IRoomItem> ItemsById { get; } = [];
    public Dictionary<RoomObjectId, IRoomAvatar> AvatarsByObjectId { get; } = [];
    public Dictionary<PlayerId, RoomObjectId> AvatarsByPlayerId { get; } = [];
    public Dictionary<int, RoomObjectId> AvatarsByPetId { get; } = [];
    public Dictionary<int, RoomObjectId> AvatarsByBotId { get; } = [];
    public Dictionary<PlayerId, string> OwnerNamesById { get; } = [];

    public RoomModelSnapshot? Model { get; internal set; } = null;
    public Altitude[] TileHeights { get; internal set; } = [];
    public short[] TileEncodedHeights { get; internal set; } = [];
    public RoomTileFlags[] TileFlags { get; internal set; } = [];
    public RoomObjectId[] TileHighestFloorItems { get; internal set; } = [];
    public HashSet<RoomObjectId>[] TileFloorStacks { get; internal set; } = [];
    public HashSet<RoomObjectId>[] TileAvatarStacks { get; internal set; } = [];

    public HashSet<PlayerId> PlayerIdsWithRights { get; } = [];
    public Dictionary<PlayerId, DateTime> MutedUntilByPlayerId { get; } = [];
    public Dictionary<PlayerId, DateTime> BannedUntilByPlayerId { get; } = [];
    public Dictionary<string, PlayerId> DoorbellRingersByName { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public bool IsRoomMuted { get; internal set; } = false;

    /// <summary>Set once deletion starts: no new entries, and hydration must not resurrect it.</summary>
    public bool IsDeleting { get; internal set; } = false;
    public HashSet<string> FilterWords { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<RoomObjectId, FriendFurniLockRequest> PendingFriendFurniLocks { get; } = [];

    /// <summary>Nest breedings awaiting the owners' answers, keyed by the nest item.</summary>
    public Dictionary<RoomObjectId, NestBreedingSession> PendingNestBreedings { get; } = [];

    /// <summary>Monsterplant breedings awaiting the invited owner, keyed by the requesting plant.</summary>
    public Dictionary<int, PlantBreedingRequest> PendingPlantBreedings { get; } = [];
    public bool IsFilterLoaded { get; internal set; } = false;
    public bool IsPetsLoaded { get; internal set; } = false;
    public bool IsBotsLoaded { get; internal set; } = false;
    public HashSet<PlayerId> PlayerIdsWhoRated { get; } = [];

    /// <summary>Navigator-visible data changed since the room became active.</summary>
    public bool IsListingChanged { get; internal set; } = false;

    public HashSet<int> DirtyHeightTileIds { get; set; } = [];
    public HashSet<RoomObjectId> DirtyItemIds { get; set; } = [];
    public HashSet<RoomObjectId> DirtyFloorItemIds { get; set; } = [];
    public HashSet<RoomObjectId> DirtyWallItemIds { get; set; } = [];

    public WiredVariableHash AllVariablesHash { get; internal set; } = new WiredVariableHash(0);

    public Dictionary<RoomPropertyType, string> RoomProperties { get; } = [];

    public bool IsMapReady { get; internal set; } = false;
    public bool IsFurniLoaded { get; internal set; } = false;
    public bool IsTileComputationPaused { get; internal set; } = false;
    public bool IsRightsLoaded { get; internal set; } = false;
    public bool IsMutesLoaded { get; internal set; } = false;
    public bool IsBansLoaded { get; internal set; } = false;

    public long EpochMs { get; set; } = 0;
    public long NextAvatarBoundaryMs { get; set; } = 0;
    public long NextRollerBoundaryMs { get; set; } = 0;
    public long NextWiredBoundaryMs { get; set; } = 0;
}
