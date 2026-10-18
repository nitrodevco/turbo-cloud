using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Achievements.Configuration;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.RewardTracks;

namespace Turbo.Achievements;

/// <summary>
/// Counts reward track tasks from the achievement facts the hotel already records: each fact
/// source below is the action a task of that type counts. A visit counts once per room, as the
/// explore achievement does. Only facts some task of an enabled track counts reach the player's grain.
/// </summary>
public sealed class RewardTrackFactListener : IAchievementFactListener
{
    private static readonly Dictionary<string, (string ActionType, bool Distinct)> ACTIONS = new()
    {
        [AchievementSources.VISIT] = (RewardTrackActionTypes.ENTER_OTHER_USERS_ROOM, true),
        [AchievementSources.FURNITURE] = (RewardTrackActionTypes.SWITCH_ITEM_STATE, false),
        [AchievementSources.FIGURE] = (RewardTrackActionTypes.CHANGE_FIGURE, false),
        [AchievementSources.MOTTO] = (RewardTrackActionTypes.CHANGE_MOTTO, false),
        [AchievementSources.RESPECT_GIVEN] = (RewardTrackActionTypes.GIVE_RESPECT, false),
        [AchievementSources.PET_RESPECT_GIVEN] = (RewardTrackActionTypes.PET_RESPECT, false),
        [AchievementSources.PET_LEVEL] = (RewardTrackActionTypes.PET_LEVEL, false),
        [AchievementSources.NUTRITION] = (RewardTrackActionTypes.PET_EAT, false),
        [AchievementSources.BADGE_WORN] = (RewardTrackActionTypes.WEAR_BADGE, false),
    };

    private readonly IGrainFactory _grainFactory;
    private readonly bool _enabled;
    private readonly IRewardTrackStore _tracks;
    private readonly ILogger<RewardTrackFactListener> _logger;

    public RewardTrackFactListener(
        IOptions<RewardTrackConfig> config,
        IRewardTrackStore tracks,
        IGrainFactory grainFactory,
        ILogger<RewardTrackFactListener> logger
    )
    {
        _grainFactory = grainFactory;
        _logger = logger;
        _enabled = config.Value.Enabled;
        _tracks = tracks;
    }

    public void OnFactRecorded(PlayerId playerId, AchievementFact fact)
    {
        if (
            !ACTIONS.TryGetValue(fact.Source, out var action)
            || !_enabled
            || !_tracks.Counts(action.ActionType)
        )
            return;

        _grainFactory
            .GetPlayerRewardTrackGrain(playerId)
            .RecordActionAsync(
                action.ActionType,
                action.Distinct ? fact.Value : "",
                CancellationToken.None
            )
            .LogAndForget(
                _logger,
                "count {ActionType} of player {PlayerId} for reward tracks",
                action.ActionType,
                playerId
            );
    }
}
