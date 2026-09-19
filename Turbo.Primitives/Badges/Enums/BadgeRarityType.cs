namespace Turbo.Primitives.Badges.Enums;

/// <summary>
/// How rare a badge is, as the client's <c>BadgeRarity</c> numbers it. The client names only
/// four of the seven constants (COMMON, RARE, VERY_RARE, MYTHICAL); the other names here come
/// from the localisation keys it maps each id to (badge.rarity.uncommon, .epic, .legendary,
/// .unique). From <see cref="Rare"/> up a badge gets its own group, glow and leaderboard. The
/// client shows <see cref="Uncommon"/> as its own tier only when its "badge_rarity.uncommon"
/// setting is on; otherwise it is drawn as common.
///
/// The client never decides a rarity: the server sends it with every badge. How owner counts
/// map to tiers is therefore hotel data (<c>BadgeConfig</c>), not something read from the client.
/// </summary>
public enum BadgeRarityType
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Mythical = 4,
    Legendary = 5,
    Unique = 6,
}
