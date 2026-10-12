using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.Navigator.Configuration;

public class NavigatorConfig
{
    public const string SECTION_NAME = "Turbo:Navigator";

    /// <summary>Rooms shown in each block of a top-level view before "show more".</summary>
    public int BlockPreviewLimit { get; init; } = 10;

    /// <summary>Rooms returned for a single full search.</summary>
    public int SearchResultLimit { get; init; } = 100;

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
