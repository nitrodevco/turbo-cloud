using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

internal sealed class RoomLiveState
{
    public required RoomId RoomId { get; init; }
    public RoomSnapshot RoomSnapshot { get; set; } = default!;

    public Dictionary<RoomObjectId, IRoomItem> ItemsById { get; } = [];

    /// <summary>Counts up each time an item enters or leaves the room; caches of "which of these are here" key on it.</summary>
    public long ItemsVersion { get; set; }
    public Dictionary<RoomObjectId, IRoomAvatar> AvatarsByObjectId { get; } = [];
    public Dictionary<PlayerId, RoomObjectId> AvatarsByPlayerId { get; } = [];
    public Dictionary<int, RoomObjectId> AvatarsByPetId { get; } = [];
    public Dictionary<int, RoomObjectId> AvatarsByBotId { get; } = [];

    /// <summary>
    /// The pets and bots among <see cref="AvatarsByObjectId"/>, kept as they attach and detach,
    /// so the ticks that walk them every boundary do not filter every avatar in the room.
    /// </summary>
    public List<IRoomPet> Pets { get; } = [];
    public List<IRoomBot> Bots { get; } = [];
    public Dictionary<PlayerId, string> OwnerNamesById { get; } = [];

    public RoomModelSnapshot? Model { get; internal set; } = null;
    public Altitude[] TileHeights { get; internal set; } = [];
    public short[] TileEncodedHeights { get; internal set; } = [];
    public RoomTileFlags[] TileFlags { get; internal set; } = [];
    public RoomObjectId[] TileHighestFloorItems { get; internal set; } = [];
    public HashSet<RoomObjectId>[] TileFloorStacks { get; internal set; } = [];
    public HashSet<RoomObjectId>[] TileAvatarStacks { get; internal set; } = [];

    public HashSet<PlayerId> PlayerIdsWithRights { get; } = [];

    /// <summary>
    /// In a group homeroom, each player's controller level as the group last answered it; see
    /// <c>RoomSecurityModule.GetGroupLevelAsync</c> for what forgets an entry.
    /// </summary>
    public Dictionary<PlayerId, RoomControllerType> GroupLevelByPlayerId { get; } = [];

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

    /// <summary>Pets and bots whose row is out of date, by pet or bot id; handed over with the items.</summary>
    public HashSet<int> DirtyPetIds { get; } = [];
    public HashSet<int> DirtyBotIds { get; } = [];

    /// <summary>
    /// Chat lines not yet handed to the persistence grain, oldest first, bounded by
    /// <c>RoomConfig.MaxPendingChatlogs</c>.
    /// </summary>
    public Queue<RoomChatlogSnapshot> PendingChatlogs { get; } = new();

    /// <summary>The id the next temporary furni gets; they count down from -1 and are never reused.</summary>
    public int NextTemporaryItemId { get; set; } = -1;
    public HashSet<RoomObjectId> DirtyFloorItemIds { get; set; } = [];
    public HashSet<RoomObjectId> DirtyWallItemIds { get; set; } = [];

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

    /// <summary>When the room next hands what changed to its persistence grain.</summary>
    public long NextPersistenceBoundaryMs { get; set; } = 0;
}
