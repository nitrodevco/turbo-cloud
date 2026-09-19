using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Throws the selected users out of the room after whispering them the string param. The room
/// owner is never kicked. The room drops the avatar, then the presence closes the session
/// through <c>OnRemovedFromRoomAsync</c>, the eviction call that never re-enters the room. It is
/// not awaited: a wired tick must not wait on a presence that may itself be waiting on the room.
/// </summary>
[RoomObjectLogic("wf_act_kick_user")]
public class WiredActionKickUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.KICK_FROM_ROOM;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override int GetStringParamMaxLength() =>
        _roomGrain._roomConfig.WiredKickMessageMaxLength;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var message = _wiredData.StringParam?.Trim() ?? string.Empty;
        message = await ctx.FormatTextAsync(message, ct);

        var ownerId = _roomGrain._state.RoomSnapshot.OwnerId;
        var kicked = false;

        foreach (var player in GetPlayers(ctx.GetSelection(this)))
        {
            if (player.PlayerId == ownerId)
                continue;

            if (message.Length > 0)
                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    player.PlayerId,
                    new WhisperMessageComposer
                    {
                        ObjectId = player.ObjectId,
                        Text = message,
                        Gesture = AvatarGestureType.None,
                        StyleId = 0,
                        Links = [],
                        TrackingId = -1,
                        ReceiverRoomIndex = player.ObjectId,
                    },
                    ct
                );

            await _roomGrain.AvatarModule.RemoveAvatarFromPlayerAsync(
                ctx.AsActionContext(),
                player.PlayerId,
                ct
            );

            _grainFactory
                .GetPlayerPresenceGrain(player.PlayerId)
                .OnRemovedFromRoomAsync(_roomGrain.RoomId, true, CancellationToken.None)
                .LogAndForget(
                    _roomGrain._logger,
                    $"close the room session of player {player.PlayerId} kicked by wired from room {_roomGrain.RoomId}"
                );

            kicked = true;
        }

        return kicked;
    }
}
