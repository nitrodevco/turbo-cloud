using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One room as staff inspect it: its settings as saved, whether it is loaded and who is in it,
/// who holds rights or a ban, and what the viewer may do to it. A password is never sent, only
/// whether there is one; <c>IsMuted</c> is null for a room that is not loaded.
/// </summary>
public sealed record RoomDetailResponse(
    int Id,
    string Name,
    string Description,
    int OwnerId,
    string OwnerName,
    string Model,
    int? CategoryId,
    string? CategoryName,
    IReadOnlyList<string> Tags,
    string DoorMode,
    bool HasPassword,
    int PlayersMax,
    string TradeMode,
    bool AllowPets,
    bool AllowPetsEat,
    bool AllowWalkThrough,
    string WhoCanMute,
    string WhoCanKick,
    string WhoCanBan,
    string ChatFloodProtection,
    bool HideWalls,
    string WallThickness,
    string FloorThickness,
    bool LeaveOnDoorTile,
    bool IdleSleepEnabled,
    int IdleSleepTimeoutSeconds,
    bool IdleAutokickEnabled,
    int IdleAutokickTimeoutSeconds,
    bool MuteAllPets,
    bool StaffPick,
    bool HiddenByBuildersClub,
    int Score,
    DateTime CreatedAtUtc,
    DateTime LastActiveUtc,
    bool IsLoaded,
    bool? IsMuted,
    IReadOnlyList<RoomPlayerRef> PlayersInside,
    IReadOnlyList<RoomPlayerRef> RightsHolders,
    IReadOnlyList<RoomBanItem> Bans,
    RoomAbilities Can
);
