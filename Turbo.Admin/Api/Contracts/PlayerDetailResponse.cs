using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One player as staff look them up: profile, where they are now, wallet, rooms and sanctions, and
/// the Discord account they sign in to the public site with, when they have one.
/// </summary>
public sealed record PlayerDetailResponse(
    int Id,
    string Name,
    string? Motto,
    string Figure,
    string Gender,
    bool IsOnline,
    PlayerRoomRef? CurrentRoom,
    DateTime? LastLoginUtc,
    DateTime JoinedUtc,
    int RespectPoints,
    IReadOnlyList<PlayerCurrencyItem> Currencies,
    int RoomsOwned,
    IReadOnlyList<PlayerRoomRef> RecentRooms,
    IReadOnlyList<PlayerSanctionItem> Sanctions,
    PlayerDiscordInfo? Discord
);
