namespace Turbo.Primitives.Achievements;

/// <summary>Version-one authoritative hotel facts. No packet can submit these.</summary>
public static class AchievementSources
{
    public const string ONLINE = "presence.online";
    public const string LOGIN = "identity.login";
    public const string ACCOUNT_AGE = "identity.account-age";
    public const string FIGURE = "identity.figure-change";
    public const string MOTTO = "identity.motto-change";

    /// <summary>A badge put on (into a slot), its value the badge code. One fact per badge.</summary>
    public const string BADGE_WORN = "identity.badge-worn";
    public const string HC = "membership.eligible-seconds";
    public const string PURCHASED_HC = "membership.purchased-days";
    public const string VISIT = "explore.admitted-room";
    public const string FURNITURE = "explore.furniture-use";
    public const string RESPECT_GIVEN = "social.respect-given";
    public const string RESPECT_RECEIVED = "social.respect-received";
    public const string PETS = "pets.owned";
    public const string NUTRITION = "pets.nutrition-supplied";
    public const string PET_LEVEL = "pets.level-increase";
    public const string PET_RESPECT_GIVEN = "pets.respect-given";
    public const string PET_RESPECT_RECEIVED = "pets.respect-received";
    public const string FLOOR_HEIGHTS = "builder.floor-heights";
    public const string ROOM_RANK = "builder.room-rank";

    /// <summary>
    /// A hit on a crackable furni, its value the achievement it counts towards (Sulake's
    /// <c>incrementalHitAchievementName</c>, lower case). One fact per hit.
    /// </summary>
    public const string CRACKABLE_HIT = "crackables.hit";

    /// <summary>
    /// The hit that cracked a crackable furni, its value the achievement it counts towards
    /// (<c>finalHitAchievementName</c>, lower case) and its amount <c>finalHitAchievementCount</c>.
    /// </summary>
    public const string CRACKABLE_CRACKED = "crackables.cracked";

    /// <summary>
    /// A placeholder for achievements nothing records facts for yet, such as the Habbo ones the
    /// hotel has no gameplay for. A definition on it can be listed or archived but never enabled;
    /// until it is moved to a source something records, no player can progress it.
    /// </summary>
    public const string UNHOOKED = "catalog.unhooked";
}
