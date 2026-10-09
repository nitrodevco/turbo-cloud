using System.Collections.Generic;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Players;

namespace Turbo.Achievements.Grains;

internal sealed class PlayerRewardTrackLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The player's row for each track they have progress on, by track id.</summary>
    public Dictionary<string, PlayerRewardTrackEntity> Rows { get; } = [];

    /// <summary>Each row's progress, read from its JSON, by track id.</summary>
    public Dictionary<string, RewardTrackProgress> Progress { get; } = [];
}
