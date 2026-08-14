using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task<RoomPointerSnapshot> GetActiveRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot
            {
                RoomId = _state.ActiveRoomId,
                ActiveSinceUtc = _state.ActiveRoomSinceUtc,
            }
        );

    public Task<RoomPendingSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new RoomPendingSnapshot
            {
                RoomId = _state.PendingRoomId,
                Approved = _state.PendingRoomApproved,
            }
        );

    public async Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct)
    {
        if (roomId <= 0)
            return;

        await ClearActiveRoomAsync(ct);

        var next = roomId;

        _state.ActiveRoomId = next;
        _state.PendingRoomId = -1;
        _state.PendingRoomApproved = false;
        _state.ActiveRoomSinceUtc = DateTime.UtcNow;

        await _grainFactory.GetRoomDirectoryGrain().AddPlayerToRoomAsync(_state.PlayerId, next, ct);

        var provider = this.GetStreamProvider(OrleansStreamProviders.ROOM_STREAM_PROVIDER);
        var streamId = StreamId.Create(OrleansStreamNames.ROOM_STREAM, roomId.Value);
        var stream = provider.GetStream<RoomOutboundSnapshot>(streamId);

        _roomOutboundSub = await stream.SubscribeAsync(this);

        var room = _grainFactory.GetRoomGrain(roomId);

        var playerSnapshot = await _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .GetSummaryAsync(ct);

        var ctx = new ActionContext
        {
            Origin = ActionOrigin.Player,
            SessionKey = SessionKey.Invalid,
            PlayerId = _state.PlayerId,
            RoomId = roomId,
        };

        await room.CreateAvatarFromPlayerAsync(ctx, playerSnapshot, ct);
    }

    public async Task ClearActiveRoomAsync(CancellationToken ct)
    {
        if (_state.ActiveRoomId <= 0)
            return;

        var prev = _state.ActiveRoomId;

        _state.ActiveRoomId = -1;
        _state.ActiveRoomSinceUtc = DateTime.UtcNow;

        var ctx = new ActionContext
        {
            Origin = ActionOrigin.Player,
            SessionKey = SessionKey.Invalid,
            PlayerId = _state.PlayerId,
            RoomId = prev,
        };

        var roomGrain = _grainFactory.GetRoomGrain(prev);

        await roomGrain.RemoveAvatarFromPlayerAsync(ctx, ctx.PlayerId, ct);

        await _grainFactory
            .GetRoomDirectoryGrain()
            .RemovePlayerFromRoomAsync(ctx.PlayerId, prev, ct);

        if (_roomOutboundSub is not null)
        {
            await _roomOutboundSub.UnsubscribeAsync();

            _roomOutboundSub = null;
        }
    }

    public Task SetPendingRoomAsync(RoomId roomId, bool approved)
    {
        _state.PendingRoomId = roomId;
        _state.PendingRoomApproved = approved;

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
                new WiredPermissionsEventMessageComposer { CanModify = true, CanRead = true }
            );

            if (controllerType >= RoomControllerType.Owner)
                await SendComposerAsync(new YouAreOwnerMessageComposer { RoomId = roomId });

            return;
        }

        await SendComposerAsync(new YouAreNotControllerMessageComposer { });
    }
}
