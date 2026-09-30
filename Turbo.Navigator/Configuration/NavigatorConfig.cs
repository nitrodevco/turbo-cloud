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
}
