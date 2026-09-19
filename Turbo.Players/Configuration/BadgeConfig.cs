using System.Collections.Generic;

namespace Turbo.Players.Configuration;

/// <summary>
/// Badge rarity and leaderboard tunables. The client only draws a rarity the server sends, so
/// how common a badge must be for each tier is the hotel's decision and lives here. A tier
/// applies when the share of registered players owning the badge is at or below its limit;
/// the rarest tier that fits wins. A row in <c>badge_definitions</c> overrides all of it.
/// </summary>
public class BadgeConfig
{
    public const string SECTION_NAME = "Turbo:Badges";

    /// <summary>
    /// Below this many registered players every badge is common unless pinned: in a hotel of
    /// ten, one owner is a tenth of everyone, not a sign of rarity.
    /// </summary>
    public int MinimumPlayersForRarity { get; init; } = 100;

    /// <summary>A badge this many players or fewer own is unique.</summary>
    public int UniqueMaxOwners { get; init; } = 1;

    public double LegendaryMaxOwnerShare { get; init; } = 0.001;
    public double MythicalMaxOwnerShare { get; init; } = 0.005;
    public double EpicMaxOwnerShare { get; init; } = 0.02;
    public double RareMaxOwnerShare { get; init; } = 0.08;
    public double UncommonMaxOwnerShare { get; init; } = 0.25;

    /// <summary>How often owner counts are recounted from the database; grants adjust them in between.</summary>
    public int OwnerCountRefreshMs { get; init; } = 300000;

    /// <summary>
    /// How long a board chunk, and the score table ranks are read from, stay cached. The client
    /// treats a chunk as stale after a minute, so a shorter cache buys nothing.
    /// </summary>
    public int LeaderboardCacheMs { get; init; } = 60000;
    public int LeaderboardMaxChunkSize { get; init; } = 50;
    public int LeaderboardMaxCachedChunks { get; init; } = 200;

    /// <summary>
    /// Badges a client may ask for by request code (the landing view's "request badge" button):
    /// request code to badge code. Nothing is requestable unless listed.
    /// </summary>
    public Dictionary<string, string> RequestableBadges { get; init; } = [];
}
