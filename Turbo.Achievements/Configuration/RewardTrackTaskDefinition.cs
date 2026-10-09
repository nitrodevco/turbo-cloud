using System.Collections.Generic;

namespace Turbo.Achievements.Configuration;

/// <summary>
/// A task: <see cref="Id"/> names the <c>reward_track.&lt;track&gt;.task.&lt;id&gt;.*</c> texts,
/// <see cref="ActionType"/> is one of <c>RewardTrackActionTypes</c>.
/// </summary>
public sealed class RewardTrackTaskDefinition
{
    public string Id { get; set; } = "";

    public string ActionType { get; set; } = "";

    public string Parameter { get; set; } = "";

    /// <summary>Counts only for a player who owns the track's premium.</summary>
    public bool Premium { get; set; }

    /// <summary>In order of their counts; each one reached gives its points.</summary>
    public List<RewardTrackTaskLevelDefinition> Levels { get; set; } = [];
}
