using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One player as staff look them up: profile, where they are now, wallet, how many rooms they own,
/// sanctions, and the Discord account they sign in to the public site with, when they have one. A
/// silence and a trade lock are not sanctions but denials of a node on the player;
/// <c>Silenced</c> and <c>TradeLocked</c> are the ones that hold now, null when none does.
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
    IReadOnlyList<PlayerSanctionItem> Sanctions,
    PlayerDiscordInfo? Discord,
    PlayerRestrictionItem? Silenced,
    PlayerRestrictionItem? TradeLocked
);
