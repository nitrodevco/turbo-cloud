using System.Collections.Generic;

namespace Turbo.Players.Grains.Badges;

/// <summary>The leaderboards are one grain for the hotel, so their state carries no key.</summary>
internal sealed class BadgeLeaderboardLiveState
{
    /// <summary>
    /// The boards held in memory. The total badges board is always here once read, since every
    /// avatar's rank is read from it; the others only while somebody asks for them.
    /// </summary>
    public Dictionary<BadgeLeaderboardKey, BadgeLeaderboardBoard> Boards { get; } = [];

    /// <summary>Boards asked for since the last refresh: the ones that refresh reads again.</summary>
    public HashSet<BadgeLeaderboardKey> RequestedBoards { get; } = [];
}
