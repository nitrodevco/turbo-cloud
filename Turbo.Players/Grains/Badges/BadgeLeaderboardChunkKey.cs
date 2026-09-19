using Turbo.Primitives.Badges.Enums;

namespace Turbo.Players.Grains.Badges;

/// <summary>What identifies a cached leaderboard chunk.</summary>
internal readonly record struct BadgeLeaderboardChunkKey(
    BadgeLeaderboardType Type,
    int Rarity,
    int ChunkIndex,
    int ChunkSize
);
