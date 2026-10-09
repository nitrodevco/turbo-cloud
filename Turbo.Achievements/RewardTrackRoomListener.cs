using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Achievements.Configuration;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Events.RoomItem;

namespace Turbo.Achievements;

/// <summary>
/// Counts the reward track tasks no achievement fact covers from what players do in rooms: a
/// wave (<c>wave</c>), starting a dance (<c>dance</c>) and putting a furni down (<c>place_item</c>),
/// the action types of the official client's <c>reward_track_tasks_&lt;type&gt;</c> images. Only a
/// player's own doing counts, never wired's or a bot's, and only types some configured task counts
/// reach the player's grain. The room listener processor finds and registers it, as it does a
/// plugin's.
/// </summary>
public sealed class RewardTrackRoomListener : IRoomEventListener
{
    private readonly IGrainFactory _grainFactory;
    private readonly HashSet<string> _counted;
    private readonly ILogger<RewardTrackRoomListener> _logger;

    public RewardTrackRoomListener(
        IOptions<RewardTrackConfig> config,
        IGrainFactory grainFactory,
        ILogger<RewardTrackRoomListener> logger
    )
    {
        _grainFactory = grainFactory;
        _logger = logger;
        _counted = config.Value.Enabled
            ? config
                .Value.Tracks.SelectMany(x => x.Tasks)
                .Select(x => x.ActionType.ToLowerInvariant())
                .ToHashSet()
            : [];
    }

    /// <summary>The action type a room event counts as, or null.</summary>
    public static string? ActionTypeOf(RoomEvent evt) =>
        evt switch
        {
            AvatarPerformsActionEvent
            {
                ActionType: AvatarActionType.Expression,
                Value: (int)AvatarExpressionType.Wave,
            } => RewardTrackActionTypes.WAVE,
            AvatarPerformsActionEvent { ActionType: AvatarActionType.Dance, Value: > 0 } =>
                RewardTrackActionTypes.DANCE,
            RoomItemPlacedEvent => RewardTrackActionTypes.PLACE_ITEM,
            _ => null,
        };

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (
            evt.CausedBy is not { Origin: ActionOrigin.Player, PlayerId: var playerId }
            || playerId <= 0
            || ActionTypeOf(evt) is not { } actionType
            || !_counted.Contains(actionType)
        )
            return Task.CompletedTask;

        // Handed to the player's grain without waiting: a listener runs in the room's turn.
        _grainFactory
            .GetPlayerRewardTrackGrain(playerId)
            .RecordActionAsync(actionType, "", CancellationToken.None)
            .LogAndForget(
                _logger,
                "count {ActionType} of player {PlayerId} for reward tracks",
                actionType,
                playerId
            );

        return Task.CompletedTask;
    }
}
