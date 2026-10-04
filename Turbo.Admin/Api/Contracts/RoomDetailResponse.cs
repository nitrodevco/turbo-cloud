using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One room as staff inspect it: its settings as saved, whether it is loaded and who is in it,
/// and who holds rights or a ban. A password is never sent, only whether there is one.
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
    bool StaffPick,
    bool HiddenByBuildersClub,
    int Score,
    DateTime CreatedAtUtc,
    DateTime LastActiveUtc,
    bool IsLoaded,
    IReadOnlyList<RoomPlayerRef> PlayersInside,
    IReadOnlyList<RoomPlayerRef> RightsHolders,
    IReadOnlyList<RoomBanItem> Bans
);
