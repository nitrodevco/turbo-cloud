using System;
using System.Collections.Generic;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Players.Grains.Badges;

/// <summary>The directory is one grain for the hotel, so its state carries no key.</summary>
internal sealed class BadgeDirectoryLiveState
{
    public Dictionary<string, int> OwnerCountByCode { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rarities pinned in <c>badge_definitions</c>, which win over the owner count.</summary>
    public Dictionary<string, BadgeRarityType> PinnedRarityByCode { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registered players: what an owner count is a share of.</summary>
    public int TotalPlayers { get; set; }
}
