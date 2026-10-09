using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests;

namespace Turbo.Achievements;

/// <summary>
/// Counts quests from the achievement facts the hotel already records: each fact source below
/// is what a quest of that type counts, with the fact's value for a quest with a target (the
/// badge worn).
/// </summary>
public sealed class QuestFactListener(IGrainFactory grainFactory, ILogger<QuestFactListener> logger)
    : IAchievementFactListener
{
    private static readonly Dictionary<string, string> TYPES = new()
    {
        [AchievementSources.BADGE_WORN] = QuestTypes.WEAR_BADGE,
        [AchievementSources.NUTRITION] = QuestTypes.PET_EAT,
    };

    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ILogger<QuestFactListener> _logger = logger;

    public void OnFactRecorded(PlayerId playerId, AchievementFact fact)
    {
        if (!TYPES.TryGetValue(fact.Source, out var type))
            return;

        _grainFactory
            .GetPlayerQuestGrain(playerId)
            .RecordAsync(type, fact.Value, CancellationToken.None)
            .LogAndForget(_logger, "count {Type} of player {PlayerId} for quests", type, playerId);
    }
}
