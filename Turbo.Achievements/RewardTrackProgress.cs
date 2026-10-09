using System.Collections.Generic;

namespace Turbo.Achievements;

/// <summary>What <c>player_reward_tracks.progress_json</c> holds for one track.</summary>
internal sealed class RewardTrackProgress
{
    /// <summary>Each task's count, by task id.</summary>
    public Dictionary<string, int> Tasks { get; set; } = [];

    /// <summary>The values a distinct task has counted (room ids for visits), by task id.</summary>
    public Dictionary<string, List<string>> Counted { get; set; } = [];

    public List<string> Claimed { get; set; } = [];
}
