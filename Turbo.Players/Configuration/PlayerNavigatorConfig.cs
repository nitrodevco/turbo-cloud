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
}
