using System;
using System.Collections.Generic;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.Navigator.Configuration;

public class NavigatorConfig
{
    public const string SECTION_NAME = "Turbo:Navigator";

    /// <summary>
    /// Rooms shown in each block of a top-level view before "show more", unless
    /// <see cref="BlockPreviewLimits"/> names the block. Habbo shows 9 (its hotel view's
    /// category blocks and My World's history, official client capture 2026-10-13/16).
    /// </summary>
    public int BlockPreviewLimit { get; init; } = 9;

    /// <summary>
    /// Preview sizes by block code. Habbo's hotel view (official client capture 2026-10-13/16,
    /// six samples): hot 12, popular 12, recommended 7.
    /// </summary>
    public Dictionary<string, int> BlockPreviewLimits { get; init; } =
        new(StringComparer.Ordinal)
        {
            [NavigatorSearchCodes.HOT] = 12,
            [NavigatorSearchCodes.POPULAR] = 12,
            [NavigatorSearchCodes.RECOMMENDED] = 7,
        };

    /// <summary>Rooms returned for a single full search.</summary>
    public int SearchResultLimit { get; init; } = 100;

    /// <summary>
    /// Full-search sizes by search code, where Habbo caps one below
    /// <see cref="SearchResultLimit"/>: its "popular" search returns 75 rooms (official client
    /// capture 2026-10-16, the 75th still had 2 users).
    /// </summary>
    public Dictionary<string, int> SearchResultLimits { get; init; } =
        new(StringComparer.Ordinal) { [NavigatorSearchCodes.POPULAR] = 75 };

    /// <summary>
    /// Rooms in a full category search (<c>category__&lt;name&gt;</c>, the category's "show
    /// more"): 75 in Habbo's (official client capture 2026-10-16).
    /// </summary>
    public int CategorySearchResultLimit { get; init; } = 75;

    /// <summary>
    /// Room categories the hotel view's "recommended" block draws from; empty draws from every
    /// category. Habbo's block holds occupied open rooms by population, but which rooms it
    /// considers is its own (mostly one category for the recorded player), so the pool is left
    /// to the hotel.
    /// </summary>
    public int[] RecommendedCategoryIds { get; init; } = [];

    /// <summary>
    /// Tops the popular rooms up with the best-rated empty rooms when fewer are occupied, so a
    /// quiet hotel's navigator is not empty. Habbo lists occupied rooms only, so it is off.
    /// </summary>
    public bool PopularFillWithTopRated { get; init; }

    // The per-player limits of this section (history, favourites, saved searches, collapsed
    // codes, view modes, the search quota) are bound into Turbo.Players' PlayerNavigatorConfig,
    // since the grain that enforces them lives there.
    public int MaxRoomsPerPlayer { get; init; } = 50;
    public int RoomNameMinLength { get; init; } = 3;
    public int RoomNameMaxLength { get; init; } = 60;
    public int RoomDescriptionMaxLength { get; init; } = 128;
    public int MaxPlayersLimit { get; init; } = 50;

    /// <summary>
    /// Longest search code or filter echoed back and used as a cache key. What a player may
    /// save is bounded separately, by the columns it is stored in.
    /// </summary>
    public int MaxSearchCodeLength { get; init; } = 64;
    public int MaxTagsPerRoom { get; init; } = 2;
    public int MaxTagLength { get; init; } = 30;
    public int PopularTagsLimit { get; init; } = 50;
    public int RoomEventDurationMinutes { get; init; } = 120;

    /// <summary>
    /// How long a listing or search result is kept before it is reloaded anyway. Changes made
    /// through the emulator evict entries immediately via the room directory, so this only bounds
    /// memory and picks up edits made directly in the database.
    /// </summary>
    public int ResultCacheSeconds { get; init; } = 600;

    /// <summary>How long a room looked up by id (favourites, history) is kept.</summary>
    public int RoomCacheSeconds { get; init; } = 600;

    /// <summary>Maximum cached entries (rooms and result lists) kept per silo.</summary>
    public int CacheSizeLimit { get; init; } = 10000;

    /// <summary>
    /// My World blocks sent even when they hold no rooms; every other block is left out when
    /// empty. Unset keeps Habbo's: <c>favorites</c> (a player with no favourites still gets the
    /// block). A code no block has (<c>[""]</c>) keeps none.
    /// </summary>
    public string[]? MyWorldKeepEmpty { get; init; }

    internal static readonly string[] HabboMyWorldKeepEmpty = [NavigatorSearchCodes.FAVOURITES];

    /// <summary>
    /// The rooms of each room network, by network id: the reception's room hopper
    /// (<c>landing.view.roomhopper.network.id</c>) sends a player to one of its network's rooms.
    /// A network with no rooms listed here sends nobody anywhere.
    /// </summary>
    public Dictionary<int, int[]> RoomNetworks { get; init; } = [];

    /// <summary>The settings a newly created room starts with; Habbo's by default.</summary>
    public NewRoomConfig NewRoom { get; init; } = new();
}

/// <summary>
/// A new room's settings, as a room Habbo creates has them (recorded on hhus, 2026-10-12):
/// leave on the door tile, fall asleep after 5 minutes, leave after 15, normal chat flood.
/// </summary>
public sealed class NewRoomConfig
{
    public bool LeaveOnDoorTile { get; init; } = true;

    /// <summary>Seconds idle before an avatar falls asleep; 0 leaves idle sleep off.</summary>
    public int IdleSleepSeconds { get; init; } = 300;

    /// <summary>Seconds idle before an avatar is sent out of the room; 0 leaves it off.</summary>
    public int IdleAutokickSeconds { get; init; } = 900;

    public ChatFloodSensitivityType ChatFlood { get; init; } = ChatFloodSensitivityType.Normal;
}
