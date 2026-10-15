using System.Threading;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Quests;

public static class RewardTrackActionExtensions
{
    /// <summary>
    /// Counts one <paramref name="actionType"/> (<see cref="RewardTrackActionTypes"/>) of the
    /// player for the reward tracks, without waiting on it: the player's grain ignores a type no
    /// task counts, and what was done does not depend on the count landing.
    /// </summary>
    public static void CountRewardTrackAction(
        this IGrainFactory factory,
        PlayerId playerId,
        string actionType,
        ILogger logger
    )
    {
        if (playerId <= 0)
            return;

        factory
            .GetPlayerRewardTrackGrain(playerId)
            .RecordActionAsync(actionType, "", CancellationToken.None)
            .LogAndForget(
                logger,
                "count {ActionType} of player {PlayerId} for reward tracks",
                actionType,
                playerId
            );
    }
}
