using System;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Admin.Rooms.Contracts;

internal sealed record RoomListItem(
    int Id,
    string Name,
    string OwnerName,
    int UsersNow,
    int PlayersMax,
    bool IsActive
);

internal sealed record RoomListResponse(
    RoomListItem[] Items,
    int TotalCount,
    int Page,
    int PageSize
);

internal sealed record RoomAvatarInfo(int PlayerId, string Name, int X, int Y);

internal sealed record RoomLiveInfo(bool IsActive, int Population, RoomAvatarInfo[] Avatars);

internal sealed record RoomDetailResponse(
    int Id,
    string Name,
    string Description,
    string OwnerName,
    RoomDoorModeType DoorMode,
    bool HasPassword,
    int PlayersMax,
    ModSettingType WhoCanMute,
    ModSettingType WhoCanKick,
    ModSettingType WhoCanBan,
    RoomLiveInfo Live
);

internal sealed record UpdateRoomRequest(
    string Name,
    string Description,
    RoomDoorModeType DoorMode,
    string? Password,
    int PlayersMax,
    ModSettingType WhoCanMute,
    ModSettingType WhoCanKick,
    ModSettingType WhoCanBan
);
