using System;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Admin.Users.Contracts;

internal sealed record UserListItem(
    int Id,
    string Name,
    string Motto,
    string Figure,
    bool IsOnline,
    DateTime CreatedAt
);

internal sealed record UserListResponse(
    UserListItem[] Items,
    int TotalCount,
    int Page,
    int PageSize
);

internal sealed record UserLiveInfo(bool IsOnline, int? CurrentRoomId, DateTime? ActiveSinceUtc);

internal sealed record UserDetailResponse(
    int Id,
    string Name,
    string Motto,
    string Figure,
    AvatarGenderType Gender,
    PlayerPerkFlags PlayerPerks,
    DateTime CreatedAt,
    UserLiveInfo Live
);

internal sealed record UpdateUserRequest(
    string? Name,
    string? Motto,
    string? Figure,
    AvatarGenderType? Gender,
    PlayerPerkFlags? PlayerPerks
);
