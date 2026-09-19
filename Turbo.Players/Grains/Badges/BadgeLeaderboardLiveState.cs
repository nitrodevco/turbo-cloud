using System.Collections.Generic;
using System.Collections.Immutable;

namespace Turbo.Players.Grains.Badges;

/// <summary>The leaderboards are one grain for the hotel, so their state carries no key.</summary>
internal sealed class BadgeLeaderboardLiveState
{
    public Dictionary<BadgeLeaderboardChunkKey, BadgeLeaderboardChunk> LeaderboardChunks { get; } =
    [];

    /// <summary>How many players own each number of badges: what a total badges rank is read from.</summary>
    public ImmutableArray<(int Score, int Players)> TotalBadgesScores { get; set; } = [];

    /// <summary>In <c>Environment.TickCount64</c> time; zero until the scores are first read.</summary>
    public long TotalBadgesScoresExpireAtMs { get; set; }
}
