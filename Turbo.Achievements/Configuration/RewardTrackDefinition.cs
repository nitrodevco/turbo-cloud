using System.Collections.Generic;

namespace Turbo.Achievements.Configuration;

/// <summary>One reward track: <see cref="Id"/> names the <c>reward_track.&lt;id&gt;.*</c> texts.</summary>
public sealed class RewardTrackDefinition
{
    public string Id { get; set; } = "";

    /// <summary>blue, orange, forest_green, red or cyan (AS3 RewardTrackTheme); empty is the default.</summary>
    public string Theme { get; set; } = "";

    /// <summary>The track's premium; null for a track without one.</summary>
    public RewardTrackPremiumDefinition? Premium { get; set; }

    public List<RewardTrackTaskDefinition> Tasks { get; set; } = [];

    public List<RewardTrackPrizeDefinition> Prizes { get; set; } = [];
}
