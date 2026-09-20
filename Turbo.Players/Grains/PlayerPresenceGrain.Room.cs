using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Networking;
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

    public Task SetPendingRoomEntryAsync(
        RoomId roomId,
        RoomEntrySnapshot entry,
        CancellationToken ct
    )
    {
        _state.PendingEntryRoomId = roomId;
        _state.PendingEntry = entry;

        return Task.CompletedTask;
    }

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

        // Only the room a furni named gets the entry it named; anywhere else is a plain walk in.
        var entry =
            _state.PendingEntryRoomId == roomId ? _state.PendingEntry : RoomEntrySnapshot.Default;

        _state.PendingEntryRoomId = -1;
        _state.PendingEntry = RoomEntrySnapshot.Default;

        await _grainFactory
            .GetRoomGrain(roomId)
            .CreateAvatarFromPlayerAsync(
                ActionContext.CreateForPlayer(_state.PlayerId, roomId),
                playerSnapshot,
                entry,
                ct
            );

        // A rank moves when other players get badges too, so entering a room is when it is
        // looked at again. Told, not awaited: the presence never awaits the inventory. A rank
        // that did change comes back as OnBadgesRankChangedAsync, now that the avatar exists.
        _grainFactory
            .GetInventoryGrain(_state.PlayerId)
            .RefreshBadgesRankAsync(CancellationToken.None)
            .LogAndForget(_logger, $"refresh the badges rank of player {_state.PlayerId}");
    }

    /// <summary>
    /// Leaves the current room. The stream subscription is released first and every step is
    /// isolated, so a failing room or directory call can never leave the subscription behind
    /// for a later activation to receive items it has no observer for.
    /// </summary>
    public async Task ClearActiveRoomAsync(CancellationToken ct)
    {
        var prev = await LeaveActiveRoomAsync(ct);

        if (prev <= 0)
            return;

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
    }

    /// <summary>
    /// The room already dropped this player's avatar (kick, ban, room deletion, wired kick) and
    /// the session only has to follow. Unlike <see cref="ClearActiveRoomAsync"/> this never calls
    /// the room grain, so a room grain may invoke it without deadlocking on itself.
    /// </summary>
    public async Task OnRemovedFromRoomAsync(RoomId roomId, bool kicked, CancellationToken ct)
    {
        if (_state.ActiveRoomId != roomId)
            return;

        await LeaveActiveRoomAsync(ct);

        IReadOnlyList<IComposer> composers = kicked
            ?
            [
                new GenericErrorMessage { ErrorCode = RoomGenericErrorType.RoomKicked },
                new CloseConnectionMessageComposer(),
            ]
            : [new CloseConnectionMessageComposer()];

        await SendComposerAsync(composers, ct);
    }

    /// <summary>
    /// Forgets the active room: releases the stream, clears the pointer and leaves the room
    /// directory. Returns the room that was left, or -1 when there was none.
    /// </summary>
    private async Task<RoomId> LeaveActiveRoomAsync(CancellationToken ct)
    {
        await UnsubscribeFromRoomStreamAsync();

        if (_state.ActiveRoomId <= 0)
            return -1;

        var prev = _state.ActiveRoomId;

        _state.ActiveRoomId = -1;
        _state.ActiveRoomSinceUtc = DateTime.UtcNow;

        try
        {
            await _grainFactory
                .GetRoomDirectoryGrain()
                .RemovePlayerFromRoomAsync(_state.PlayerId, prev, ct);
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

        return prev;
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
