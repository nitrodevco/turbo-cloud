using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>What one player's guild grain holds between calls.</summary>
internal sealed class PlayerGuildLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>
    /// The groups this player is a member of. Ids only: the name and badge of each belong to
    /// the group, and the guild directory already holds them for the whole hotel, so keeping a
    /// second copy here would only be a second thing to keep in step.
    /// </summary>
    public HashSet<int> GuildIds { get; } = [];

    /// <summary>The group whose badge they wear, or null when they have picked none.</summary>
    public int? FavouriteGuildId { get; set; }
}
