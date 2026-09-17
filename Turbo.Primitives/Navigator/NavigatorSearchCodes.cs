using System;

namespace Turbo.Primitives.Navigator;

/// <summary>
/// The search codes the client sends and expects back. The top-level contexts name a tab; the
/// section codes name one result block inside it, and the client localizes a block title from
/// its code (<c>navigator.searchcode.title.&lt;code&gt;</c>) when the block carries no text.
/// </summary>
public static class NavigatorSearchCodes
{
    public const string OFFICIAL_VIEW = "official_view";
    public const string HOTEL_VIEW = "hotel_view";
    public const string MYWORLD_VIEW = "myworld_view";
    public const string ROOMADS_VIEW = "roomads_view";

    public const string OFFICIAL_ROOT = "official-root";
    public const string POPULAR = "popular";
    public const string MY_ROOMS = "my";
    public const string FAVOURITES = "favorites";
    public const string HISTORY = "history";
    public const string FREQUENT_HISTORY = "history_freq";
    public const string FRIENDS_ROOMS = "friends_rooms";
    public const string WITH_FRIENDS = "with_friends";
    public const string WITH_RIGHTS = "with_rights";
    public const string GROUPS = "groups";
    public const string TOP_PROMOTIONS = "top_promotions";
    public const string NEW_ADS = "new_ads";

    /// <summary>Room category blocks are <c>category__&lt;category name&gt;</c>.</summary>
    public const string CATEGORY_PREFIX = "category__";

    /// <summary>Event category blocks are <c>eventcategory__&lt;category name&gt;</c>.</summary>
    public const string EVENT_CATEGORY_PREFIX = "eventcategory__";

    /// <summary>The blocks of the player's own world, in the order the client shows them.</summary>
    public static readonly string[] MyWorldSections =
    [
        MY_ROOMS,
        FAVOURITES,
        GROUPS,
        HISTORY,
        FREQUENT_HISTORY,
        FRIENDS_ROOMS,
        WITH_FRIENDS,
        WITH_RIGHTS,
    ];

    public static string Category(string categoryName) => CATEGORY_PREFIX + categoryName;

    public static string EventCategory(string categoryName) => EVENT_CATEGORY_PREFIX + categoryName;

    /// <summary>The category name in a <c>category__</c> or <c>eventcategory__</c> code.</summary>
    public static string GetCategoryName(string searchCode) =>
        searchCode.StartsWith(CATEGORY_PREFIX, StringComparison.Ordinal)
            ? searchCode[CATEGORY_PREFIX.Length..]
        : searchCode.StartsWith(EVENT_CATEGORY_PREFIX, StringComparison.Ordinal)
            ? searchCode[EVENT_CATEGORY_PREFIX.Length..]
        : string.Empty;
}
