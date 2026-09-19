using System.Collections.Immutable;
using Turbo.Primitives.Badges.Snapshots;

namespace Turbo.Players.Grains.Badges;

/// <summary>A leaderboard chunk as it was read, and when it stops being fresh.</summary>
internal sealed record BadgeLeaderboardChunk(
    int TotalEntries,
    ImmutableArray<BadgeLeaderboardEntrySnapshot> Entries,
    long ExpiresAtMs
);
