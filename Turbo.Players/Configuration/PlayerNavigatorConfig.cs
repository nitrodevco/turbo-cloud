namespace Turbo.Players.Configuration;

/// <summary>
/// The limits <c>PlayerNavigatorGrain</c> enforces on one player's navigator state. They are read
/// from the navigator's own section (<c>Turbo:Navigator</c>), beside the rest of the navigator's
/// tunables, but live here because the grain that enforces them is in this module and the
/// navigator module depends on this one, not the other way round.
/// </summary>
public class PlayerNavigatorConfig
{
    public const string SECTION_NAME = "Turbo:Navigator";

    /// <summary>Most rooms a history list (recent or frequent) returns.</summary>
    public int HistoryLimit { get; init; } = 50;

    /// <summary>Favourite rooms a player may keep; the client is told this limit on login.</summary>
    public int MaxFavouriteRooms { get; init; } = 30;
    public int MaxSavedSearches { get; init; } = 50;
    public int MaxCollapsedSearchCodes { get; init; } = 50;
    public int MaxViewModes { get; init; } = 50;

    /// <summary>Text searches a player may run against the database per window; cached searches are free.</summary>
    public int SearchRateLimitCount { get; init; } = 10;
    public int SearchRateLimitWindowSeconds { get; init; } = 10;

    /// <summary>
    /// Gives a player who has never stored any navigator preference (no saved search, collapsed
    /// block or view mode) the navigator a new Habbo account starts with: the blocks below
    /// collapsed, so the All Rooms tab opens on Hot rooms alone, and the four saved searches.
    /// The seeds are stored like the player's own choices and count toward the limits above.
    /// </summary>
    public bool SeedNewPlayerPreferences { get; init; } = true;

    /// <summary>Blocks a new player starts with collapsed. Unset keeps Habbo's.</summary>
    public string[]? NewPlayerCollapsedSearchCodes { get; init; }

    /// <summary>
    /// Also collapses every room category (<c>category__&lt;name&gt;</c>) and event category
    /// (<c>eventcategory__&lt;name&gt;</c>) for a new player, as Habbo does.
    /// </summary>
    public bool NewPlayerCollapsesCategories { get; init; } = true;

    /// <summary>
    /// Search codes a new player starts with as saved searches, with no filter. Unset keeps
    /// Habbo's: official, recommended, my, favorites.
    /// </summary>
    public string[]? NewPlayerSavedSearches { get; init; }

    /// <summary>The blocks a new Habbo account has collapsed (recorded 2026-10-12, hhus).</summary>
    public static readonly string[] HabboNewPlayerCollapsedSearchCodes =
    [
        "hot",
        "highest_score",
        "friend_finding",
        "with_friends",
        "friends_rooms",
        "favorites",
        "history",
        "groups",
        "competition",
        "top_promotions",
        "new_ads",
        "with_rights",
        "my_groups",
        "recommended",
        "history_freq",
        "campaign_target",
        "metadata",
        "staffpicks",
        "sellable_room_bundles",
        "nft_accessible_rooms",
        "nft_all_rooms",
    ];

    /// <summary>The saved searches a new Habbo account has (recorded 2026-10-12, hhus).</summary>
    public static readonly string[] HabboNewPlayerSavedSearches =
    [
        "official",
        "recommended",
        "my",
        "favorites",
    ];
}
