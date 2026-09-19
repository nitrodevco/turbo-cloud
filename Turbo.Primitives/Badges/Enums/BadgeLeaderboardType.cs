namespace Turbo.Primitives.Badges.Enums;

/// <summary>
/// The boards of the client's badge leaderboard (<c>BadgeLeaderboardController</c>): its
/// dropdown lists total badges, achievement level, then one entry per rarity tier.
/// </summary>
public enum BadgeLeaderboardType
{
    TotalBadges = 0,

    /// <summary>Badges of one rarity tier; the request names the tier.</summary>
    Rarity = 1,
    AchievementLevel = 2,
}
