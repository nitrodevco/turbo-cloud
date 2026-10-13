using System.Collections.Generic;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>What one player's guild grain holds between calls.</summary>
internal sealed class PlayerGuildLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>
    /// The groups this player is a member of, by id, with their rank in each: the rank is what the
    /// worn badge carries into a room. The name and badge of each belong to the group, and the
    /// guild directory already holds them for the whole hotel, so keeping a second copy here
    /// would only be a second thing to keep in step.
    /// </summary>
    public Dictionary<int, GuildMemberRank> RankByGuildId { get; } = [];

    /// <summary>The group whose badge they wear, or null when they have picked none.</summary>
    public int? FavouriteGuildId { get; set; }
}
