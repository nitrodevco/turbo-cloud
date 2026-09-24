using System.Collections.Generic;
using Turbo.Primitives.Guilds.Snapshots;

namespace Turbo.Guilds.Grains;

/// <summary>The directory is one grain for the hotel, so its state carries no key.</summary>
internal sealed class GuildDirectoryLiveState
{
    /// <summary>Every group in the hotel, by its id.</summary>
    public Dictionary<int, GuildSummarySnapshot> SummaryByGuildId { get; } = [];

    /// <summary>
    /// Homeroom to group. This is what a room activation asks, so it is a map rather than a scan
    /// of the summaries.
    /// </summary>
    public Dictionary<int, int> GuildIdByRoomId { get; } = [];

    /// <summary>
    /// How many members each group has, for ordering the guild base search. Kept beside the
    /// summaries rather than on them because it moves with every join and the summaries do not.
    /// </summary>
    public Dictionary<int, int> MemberCountByGuildId { get; } = [];

    /// <summary>
    /// How many groups each player owns. Bounded by the number of groups, not the number of
    /// players, so it is safe to hold; a player who owns none is simply absent.
    /// </summary>
    public Dictionary<int, int> OwnedCountByPlayerId { get; } = [];

    /// <summary>
    /// What the badge editor may pick from. Hotel data, reloaded with everything else, and
    /// never empty in a seeded hotel — an editor with no parts cannot make a badge.
    /// </summary>
    public GuildEditorDataSnapshot EditorData { get; set; } =
        new()
        {
            BaseParts = [],
            SymbolParts = [],
            BadgeColors = [],
            PrimaryColors = [],
            SecondaryColors = [],
        };

    public void Clear()
    {
        SummaryByGuildId.Clear();
        GuildIdByRoomId.Clear();
        MemberCountByGuildId.Clear();
        OwnedCountByPlayerId.Clear();
    }
}
