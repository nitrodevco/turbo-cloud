using Turbo.Primitives.Badges.Enums;

namespace Turbo.Players.Grains.Badges;

/// <summary>What identifies a board: its type, and its tier for a rarity board (else -1).</summary>
internal readonly record struct BadgeLeaderboardKey(BadgeLeaderboardType Type, int Rarity);
