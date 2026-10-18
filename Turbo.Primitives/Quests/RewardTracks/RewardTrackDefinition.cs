using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Quests.RewardTracks;

/// <summary>
/// One reward track: <see cref="Id"/> names the <c>reward_track.&lt;id&gt;.*</c> texts. Treat a
/// track handed out by <see cref="IRewardTrackStore"/> as read only: it is shared.
/// </summary>
public sealed class RewardTrackDefinition
{
    public string Id { get; set; } = "";

    /// <summary>Off, players are not sent it and it counts and gives nothing.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When players start getting it (UTC), or null for at once. Habbo's protocol carries no dates:
    /// the client has a track only from the list sent at login, so a track that starts or ends
    /// while a player is online is theirs from, or until, their next login.
    /// </summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>When players stop getting it (UTC), or null for never.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>blue, orange, forest_green, red or cyan (AS3 RewardTrackTheme); empty is the default.</summary>
    public string Theme { get; set; } = "";

    /// <summary>The track's premium; null for a track without one.</summary>
    public RewardTrackPremiumDefinition? Premium { get; set; }

    public List<RewardTrackTaskDefinition> Tasks { get; set; } = [];

    public List<RewardTrackPrizeDefinition> Prizes { get; set; } = [];

    /// <summary>Whether players get it at <paramref name="utcNow"/>.</summary>
    public bool IsLiveAt(DateTime utcNow) =>
        Enabled
        && (StartsAt is not { } start || start <= utcNow)
        && (EndsAt is not { } end || utcNow < end);
}
