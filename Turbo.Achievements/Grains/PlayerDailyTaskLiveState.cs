using System;
using System.Collections.Generic;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Players;

namespace Turbo.Achievements.Grains;

internal sealed class PlayerDailyTaskLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The task day the state was read for; a call in a later day reads it again.</summary>
    public DateTime? DayStartsAt { get; set; }

    /// <summary>Every definition, enabled or not, so an older task still finds its own.</summary>
    public Dictionary<int, DailyTaskDefinitionEntity> Definitions { get; } = [];

    /// <summary>The day's tasks, then earlier days' completed and unclaimed ones.</summary>
    public List<PlayerDailyTaskEntity> Tasks { get; } = [];
}
