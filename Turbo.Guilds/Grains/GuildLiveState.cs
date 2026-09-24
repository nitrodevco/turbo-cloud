using System.Collections.Generic;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;

namespace Turbo.Guilds.Grains;

/// <summary>What one group grain holds between calls.</summary>
internal sealed class GuildLiveState
{
    /// <summary>Null until the group is loaded, and still null when there is no such group.</summary>
    public GuildSnapshot? Guild { get; set; }

    /// <summary>
    /// The roster as ids and ranks. Pending requests and blocks are in here too, told apart by
    /// their rank, which is what lets the counts below be derived rather than stored.
    /// </summary>
    public Dictionary<int, GuildMemberRank> RankByPlayerId { get; } = [];

    /// <summary>Whether the load has run. Distinguishes "no such group" from "not looked yet".</summary>
    public bool IsLoaded { get; set; }
}
