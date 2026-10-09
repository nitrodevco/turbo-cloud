using System;
using System.Collections.Generic;
using Turbo.Database.Entities.Guilds;
using Turbo.Primitives.Guilds;

namespace Turbo.Guilds.Grains;

internal sealed class GuildForumLiveState
{
    public required GuildId GuildId { get; init; }

    /// <summary>The forum's row, detached; null while the group has no forum.</summary>
    public GuildForumEntity? Forum { get; set; }

    /// <summary>When each player last posted here, for the post cooldown.</summary>
    public Dictionary<int, DateTime> LastPostAtByPlayer { get; } = [];
}
