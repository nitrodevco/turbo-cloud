using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Grains.Systems;

public sealed class RoomAvatarTickSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    private readonly List<IRoomAvatar> _avatars = [];

    public async Task ProcessAvatarsAsync(long now, CancellationToken ct)
    {
        if (now < _roomGrain._state.NextAvatarBoundaryMs)
            return;

        while (now >= _roomGrain._state.NextAvatarBoundaryMs)
            _roomGrain._state.NextAvatarBoundaryMs += _roomGrain._roomConfig.AvatarTickMs;

        var dirtySnapshots = new List<RoomAvatarSnapshot>();

        // A step can take an avatar out of the room (a furni it lands on), so the loop walks a
        // copy; the copy's list is kept rather than made again every boundary.
        _avatars.Clear();
        _avatars.AddRange(_roomGrain._state.AvatarsByObjectId.Values);

        foreach (var avatar in _avatars)
        {
            try
            {
                await AvatarModule.ProcessNextAvatarStepAsync(avatar, ct);

                if (avatar.TilePath.Count <= 0)
                {
                    if (avatar.PendingStopAtMs > 0 && now < avatar.PendingStopAtMs)
                        continue;

                    await AvatarModule.StopWalkingAsync(avatar, ct);

                    if (avatar.NeedsInvoke)
                        await MapModule.InvokeAvatarAsync(avatar, ct);
                }
                else
                {
                    await ProcessAvatarAsync(avatar, now, ct);
                }

                RevertHead(avatar, now);
                CheckIdle(avatar, now);
                await CheckIdleKickAsync(avatar, now, ct);
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Avatar {ObjectId} in room {RoomId} failed to step",
                    avatar.ObjectId,
                    _roomGrain.RoomId
                );
            }
        }

        // Ridden pets copy their rider's step, so they are updated once every rider has moved.
        await PetModule.SyncRidingPetsAsync(ct);

        foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
        {
            if (!avatar.IsDirty)
                continue;

            dirtySnapshots.Add(avatar.GetSnapshot());

            if (avatar.HasStatus(AvatarStatusType.Sign))
                avatar.RemoveStatus(AvatarStatusType.Sign);
        }

        if (dirtySnapshots.Count == 0)
            return;

        _roomGrain.SendComposerToRoomAndForget(
            new UserUpdateMessageComposer { Avatars = [.. dirtySnapshots] }
        );
    }

    /// <summary>
    /// Turns a head that looked at someone talking back to the body once its time is up, asleep
    /// or not (1/1). A head already back on the body, as a look straight ahead leaves it, sends
    /// nothing (official client capture, hhus 2026-10-17, 1/1).
    /// </summary>
    private static void RevertHead(IRoomAvatar avatar, long now)
    {
        if (avatar.HeadRevertAtMs <= 0 || now < avatar.HeadRevertAtMs)
            return;

        avatar.HeadRevertAtMs = 0;
        avatar.SetHeadRotation(avatar.Rotation);
    }

    /// <summary>
    /// Puts an avatar to sleep once it has been still for the room's idle timeout. Waking is
    /// driven by the avatar's next action through <see cref="Modules.RoomAvatarModule.TouchAvatar"/>.
    /// </summary>
    private void CheckIdle(IRoomAvatar avatar, long now)
    {
        var room = _roomGrain._state.RoomSnapshot;

        if (avatar.IsIdle || !room.IdleSleepEnabled || avatar.AvatarType != RoomObjectType.Player)
            return;

        if (avatar.LastActiveAtMs == 0)
        {
            avatar.Touch(now);

            return;
        }

        if (now - avatar.LastActiveAtMs < room.IdleSleepTimeoutSeconds * 1000L)
            return;

        avatar.SetIdle(true);

        _roomGrain.SendComposerToRoomAndForget(
            new SleepMessageComposer { ObjectId = avatar.ObjectId, IsSleeping = true }
        );

        AvatarModule.PublishIdleChanged(avatar);
    }

    /// <summary>
    /// Sends a player out of the room once they have done nothing for the room's "Auto-kick after
    /// timeout" (<c>navigator.roomsettings.idle_autokick</c>; Habbo's rooms come with it on at
    /// 900 s and sleep at 300 s: official client capture of RoomSettingsData, WIN63-202610131318).
    /// The kick is the room's own, as a wired kick is, so the owner and anyone above them stay
    /// (inference: Habbo's idle kick itself was not recorded); whoever stays counts from now again.
    /// </summary>
    private async Task CheckIdleKickAsync(IRoomAvatar avatar, long now, CancellationToken ct)
    {
        var room = _roomGrain._state.RoomSnapshot;

        if (
            !room.IdleAutokickEnabled
            || room.IdleAutokickTimeoutSeconds <= 0
            || avatar is not IRoomPlayer player
        )
            return;

        // A player who has not done anything yet counts from their first tick in the room.
        if (avatar.LastActiveAtMs == 0)
            avatar.Touch(now);

        if (now - avatar.LastActiveAtMs < room.IdleAutokickTimeoutSeconds * 1000L)
            return;

        if (!await ModerationModule.KickPlayerBySystemAsync(player.PlayerId, string.Empty, ct))
            avatar.Touch(now);
    }

    private async Task ProcessAvatarAsync(IRoomAvatar avatar, long now, CancellationToken ct)
    {
        // The path is stored goal first, so the next step is the last entry.
        var nextTileId = avatar.TilePath[^1];

        if (await TryLeaveByDoorAsync(avatar, nextTileId))
            return;

        avatar.TilePath.RemoveAt(avatar.TilePath.Count - 1);

        if (avatar.TilePath.Count == 0)
            avatar.PendingStopAtMs = _roomGrain.AlignToNextBoundary(
                now,
                _roomGrain._roomConfig.AvatarTickMs
            );

        await ValidateAvatarStepAsync(avatar, nextTileId, now, ct);
    }

    /// <summary>
    /// A player about to step onto the room's door tile, in a room set to leave there, leaves the
    /// room instead, as on Habbo (hhus, 2026-10-12: the walk stops beside the door and the player
    /// gets YouArePlayingGame false and CloseConnection, with no step onto the door).
    /// </summary>
    private async Task<bool> TryLeaveByDoorAsync(IRoomAvatar avatar, int nextTileId)
    {
        var model = _roomGrain._state.Model;

        if (
            avatar is not IRoomPlayer player
            || !_roomGrain._state.RoomSnapshot.LeaveOnDoorTile
            || model is null
            || nextTileId != MapModule.ToIdx(model.DoorX, model.DoorY)
        )
            return false;

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            player.PlayerId,
            new YouArePlayingGameMessageComposer { IsPlaying = false },
            CancellationToken.None
        );

        await AvatarModule.RemoveAvatarFromPlayerAsync(
            ActionContext.CreateForSystem(_roomGrain.RoomId),
            player.PlayerId,
            CancellationToken.None
        );

        // The presence may be waiting on this room, so the session is closed without awaiting it.
        _roomGrain
            ._grainFactory.GetPlayerPresenceGrain(player.PlayerId)
            .OnRemovedFromRoomAsync(_roomGrain.RoomId, false, CancellationToken.None)
            .LogAndForget(
                _roomGrain._logger,
                "close the room session of player {PlayerId} leaving room {RoomId} by the door",
                player.PlayerId,
                _roomGrain.RoomId
            );

        return true;
    }

    private async Task ValidateAvatarStepAsync(
        IRoomAvatar avatar,
        int nextTileId,
        long now,
        CancellationToken ct
    )
    {
        try
        {
            var isGoal = avatar.TilePath.Count == 0;
            var prevTileId = MapModule.ToIdx(avatar.X, avatar.Y);
            var (nextX, nextY) = MapModule.GetTileXY(nextTileId);
            var nextHeight = Altitude.FromValue(
                MapModule.GetTileHeightForAvatar(nextTileId) + PetModule.GetRiderLift(avatar)
            );

            // A step the map refuses (the tile was taken, or raised too high, while the avatar
            // was on its way) is the normal end of a walk, not a failure: re-route or stop and
            // say nothing; this runs for every avatar on every tick.
            if (!MapModule.CanAvatarWalkBetween(avatar, prevTileId, nextTileId, isGoal))
            {
                // A re-route, not a new walk: it spends one of this walk's few retries. Going
                // through the walk request instead counted it as the player clicking the same
                // tile again, which a busy room turned into walks refused for no reason.
                if (!isGoal && AvatarModule.TryRerouteWalk(avatar))
                {
                    await ProcessAvatarAsync(avatar, now, ct);

                    return;
                }

                await AvatarModule.StopWalkingAsync(avatar, ct);

                return;
            }

            await AvatarModule.NotifyWalkOffAsync(avatar, prevTileId, ct);

            MapModule.RemoveAvatarAtIdx(avatar, prevTileId, false);
            MapModule.AddAvatarAtIdx(avatar, nextTileId, false);

            await AvatarModule.NotifyWalkOnAsync(avatar, nextTileId, ct);

            avatar.RemoveStatus(AvatarStatusType.Lay, AvatarStatusType.Sit);
            avatar.AddStatus(
                AvatarStatusType.Move,
                $"{nextX},{nextY},{nextHeight.ToStatusString()}"
            );
            avatar.SetRotation(RotationExtensions.FromPoints(avatar.X, avatar.Y, nextX, nextY));

            avatar.NextTileId = nextTileId;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Avatar {ObjectId} failed to step in room {RoomId}; stopping the walk",
                avatar.ObjectId,
                _roomGrain.RoomId
            );

            await AvatarModule.StopWalkingAsync(avatar, ct);
        }
    }
}
