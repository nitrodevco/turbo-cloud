using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task<RoomPointerSnapshot> GetActiveRoomAsync(CancellationToken ct) =>
        Task.FromResult(
            new RoomPointerSnapshot
            {
                RoomId = _state.ActiveRoomId,
                ActiveSinceUtc = _state.ActiveRoomSinceUtc,
            }
        );

    public Task<RoomPendingSnapshot> GetPendingRoomAsync(CancellationToken ct) =>
        Task.FromResult(
            new RoomPendingSnapshot
            {
                RoomId = _state.PendingRoomId,
                State = _state.PendingRoomState,
            }
        );

    public async Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct)
    {
        if (roomId <= 0)
            return;

        await ClearActiveRoomAsync(ct);
        await ClearPendingRoomAsync(ct);

        _state.ActiveRoomId = roomId;
        _state.ActiveRoomSinceUtc = DateTime.UtcNow;

        await _grainFactory
            .GetRoomDirectoryGrain()
            .AddPlayerToRoomAsync(_state.PlayerId, roomId, ct);

        _grainFactory
            .GetPlayerNavigatorGrain(_state.PlayerId)
            .RecordRoomVisitAsync(roomId, CancellationToken.None)
            .LogAndForget(_logger, $"record visit of player {_state.PlayerId} to room {roomId}");

        var stream = GetRoomStream(roomId);

        await PurgeStaleSubscriptionsAsync(stream, roomId);

        _roomOutboundSub = await stream.SubscribeAsync(this);

        var playerSnapshot = await _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .GetSummaryAsync(ct);

        await _grainFactory
            .GetRoomGrain(roomId)
            .CreateAvatarFromPlayerAsync(
                ActionContext.CreateForPlayer(_state.PlayerId, roomId),
                playerSnapshot,
                ct
            );
    }

    /// <summary>
    /// Leaves the current room. The stream subscription is released first and every step is
    /// isolated, so a failing room or directory call can never leave the subscription behind
    /// for a later activation to receive items it has no observer for.
    /// </summary>
    public async Task ClearActiveRoomAsync(CancellationToken ct)
    {
        await UnsubscribeFromRoomStreamAsync();

        if (_state.ActiveRoomId <= 0)
            return;

        var prev = _state.ActiveRoomId;

        _state.ActiveRoomId = -1;
        _state.ActiveRoomSinceUtc = DateTime.UtcNow;

        var ctx = ActionContext.CreateForPlayer(_state.PlayerId, prev);

        try
        {
            await _grainFactory
                .GetRoomGrain(prev)
                .RemoveAvatarFromPlayerAsync(ctx, ctx.PlayerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove avatar for player {PlayerId} from room {RoomId}",
                _state.PlayerId,
                prev
            );
        }

        try
        {
            await _grainFactory
                .GetRoomDirectoryGrain()
                .RemovePlayerFromRoomAsync(ctx.PlayerId, prev, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove player {PlayerId} from room directory for room {RoomId}",
                _state.PlayerId,
                prev
            );
        }
    }

    public Task SetPendingRoomAsync(RoomId roomId, RoomEntryState state, CancellationToken ct)
    {
        _state.PendingRoomId = roomId;
        _state.PendingRoomState = state;

        return Task.CompletedTask;
    }

    public Task ClearPendingRoomAsync(CancellationToken ct)
    {
        _state.PendingRoomId = -1;
        _state.PendingRoomState = RoomEntryState.None;

        return Task.CompletedTask;
    }

    public async Task OnControllerLevelUpdatedAsync(
        RoomId roomId,
        RoomControllerType controllerType,
        bool canModifyWired,
        bool canReadWired,
        CancellationToken ct
    )
    {
        if (_state.ActiveRoomId != roomId)
            return;

        if (controllerType >= RoomControllerType.Rights)
        {
            await SendComposerAsync(
                new YouAreControllerMessageComposer
                {
                    RoomId = roomId,
                    ControllerLevel = controllerType,
                },
                ct
            );

            if (controllerType >= RoomControllerType.Owner)
                await SendComposerAsync(new YouAreOwnerMessageComposer { RoomId = roomId }, ct);
        }
        else
        {
            await SendComposerAsync(new YouAreNotControllerMessageComposer { RoomId = roomId }, ct);
        }

        await OnWiredPermissionsUpdatedAsync(roomId, canModifyWired, canReadWired, ct);
    }

    public async Task OnWiredPermissionsUpdatedAsync(
        RoomId roomId,
        bool canModifyWired,
        bool canReadWired,
        CancellationToken ct
    )
    {
        if (_state.ActiveRoomId != roomId)
            return;

        await SendComposerAsync(
            new WiredPermissionsEventMessageComposer
            {
                CanModify = canModifyWired,
                CanRead = canReadWired,
            },
            ct
        );
    }

    private IAsyncStream<RoomOutboundSnapshot> GetRoomStream(RoomId roomId)
    {
        var provider = this.GetStreamProvider(OrleansStreamProviders.ROOM_STREAM_PROVIDER);
        var streamId = StreamId.Create(OrleansStreamNames.ROOM_STREAM, roomId.Value);

        return provider.GetStream<RoomOutboundSnapshot>(streamId);
    }

    private async Task UnsubscribeFromRoomStreamAsync()
    {
        var subscription = _roomOutboundSub;

        if (subscription is null)
            return;

        _roomOutboundSub = null;

        try
        {
            await subscription.UnsubscribeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to unsubscribe player {PlayerId} from room stream {RoomId}",
                _state.PlayerId,
                _state.ActiveRoomId
            );
        }
    }

    /// <summary>
    /// Explicit stream subscriptions outlive the activation that created them. If an earlier
    /// activation of this grain died without unsubscribing, the pub-sub still routes the room's
    /// items here and Orleans drops them with "no subscriber for that stream". Clear those before
    /// subscribing again so the only subscription is the one this activation observes.
    /// </summary>
    private async Task PurgeStaleSubscriptionsAsync(
        IAsyncStream<RoomOutboundSnapshot> stream,
        RoomId roomId
    )
    {
        try
        {
            var handles = await stream.GetAllSubscriptionHandles();

            if (handles.Count == 0)
                return;

            _logger.LogWarning(
                "Purging {Count} stale room stream subscription(s) for player {PlayerId} in room {RoomId}",
                handles.Count,
                _state.PlayerId,
                roomId
            );

            foreach (var handle in handles)
                await handle.UnsubscribeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to purge stale room stream subscriptions for player {PlayerId} in room {RoomId}",
                _state.PlayerId,
                roomId
            );
        }
    }
}
