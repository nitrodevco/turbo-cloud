using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A room's settings, all of them, as the owner's settings dialog saves them. Enums by name. A password door keeps its password when <c>Password</c> is left empty.</summary>
public sealed record RoomSettingsRequest(
    string? Name,
    string? Description,
    string? DoorMode,
    string? Password,
    int MaxPlayers,
    int? CategoryId,
    IReadOnlyList<string>? Tags,
    string? TradeMode,
    bool AllowPets,
    bool AllowPetsEat,
    bool AllowWalkThrough,
    bool HideWalls,
    string? WallThickness,
    string? FloorThickness,
    string? WhoCanMute,
    string? WhoCanKick,
    string? WhoCanBan,
    string? ChatFloodProtection,
    bool LeaveOnDoorTile,
    bool IdleSleepEnabled,
    int IdleSleepTimeoutSeconds,
    bool IdleAutokickEnabled,
    int IdleAutokickTimeoutSeconds,
    bool MuteAllPets
);
