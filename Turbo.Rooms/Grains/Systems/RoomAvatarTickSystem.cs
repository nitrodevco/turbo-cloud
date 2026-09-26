using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Grains.Systems;

public sealed class RoomAvatarTickSystem(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public async Task ProcessAvatarsAsync(long now, CancellationToken ct)
    {
        if (now < _roomGrain._state.NextAvatarBoundaryMs)
            return;

        while (now >= _roomGrain._state.NextAvatarBoundaryMs)
            _roomGrain._state.NextAvatarBoundaryMs += _roomGrain._roomConfig.AvatarTickMs;

        var dirtySnapshots = new List<RoomAvatarSnapshot>();

        foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values.ToList())
        {
            try
            {
                await _roomGrain.AvatarModule.ProcessNextAvatarStepAsync(avatar, ct);

                if (avatar.TilePath.Count <= 0)
                {
                    if (avatar.PendingStopAtMs > 0 && now < avatar.PendingStopAtMs)
                        continue;

                    await _roomGrain.AvatarModule.StopWalkingAsync(avatar, ct);

                    if (avatar.NeedsInvoke)
                        await _roomGrain.MapModule.InvokeAvatarAsync(avatar, ct);
                }
                else
                {
                    await ProcessAvatarAsync(avatar, now, ct);
                }

                CheckIdle(avatar, now);
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
        await _roomGrain.PetModule.SyncRidingPetsAsync(ct);

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
    }

    private async Task ProcessAvatarAsync(IRoomAvatar avatar, long now, CancellationToken ct)
    {
        var nextTileId = avatar.TilePath[0];
        avatar.TilePath.RemoveAt(0);

        if (avatar.TilePath.Count == 0)
            avatar.PendingStopAtMs = _roomGrain.AlignToNextBoundary(
                now,
                _roomGrain._roomConfig.AvatarTickMs
            );

        await ValidateAvatarStepAsync(avatar, nextTileId, now, ct);
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
            var prevTileId = _roomGrain.MapModule.ToIdx(avatar.X, avatar.Y);
            var (nextX, nextY) = _roomGrain.MapModule.GetTileXY(nextTileId);
            var prevHeight = _roomGrain.MapModule.GetTileHeightForAvatar(prevTileId);
            var nextHeight = _roomGrain.MapModule.GetTileHeightForAvatar(nextTileId);

            // A step the map refuses is the normal end of a walk, not a failure: the tile was
            // taken or raised while the avatar was on its way. Stop and say nothing; this runs
            // for every avatar on every tick.
            if (Math.Abs(nextHeight - prevHeight) > Math.Abs(_roomGrain._roomConfig.MaxStepHeight))
            {
                await _roomGrain.AvatarModule.StopWalkingAsync(avatar, ct);

                return;
            }

            if (!_roomGrain.MapModule.CanAvatarWalkBetween(avatar, prevTileId, nextTileId, isGoal))
            {
                if (!isGoal)
                {
                    var (goalX, goalY) = _roomGrain.MapModule.GetTileXY(avatar.GoalTileId);

                    if (await _roomGrain.AvatarModule.WalkAvatarToAsync(avatar, goalX, goalY, ct))
                    {
                        await ProcessAvatarAsync(avatar, now, ct);

                        return;
                    }
                }

                await _roomGrain.AvatarModule.StopWalkingAsync(avatar, ct);

                return;
            }

            await _roomGrain.AvatarModule.NotifyWalkOffAsync(avatar, prevTileId, ct);

            _roomGrain.MapModule.RemoveAvatarAtIdx(avatar, prevTileId, false);
            _roomGrain.MapModule.AddAvatarAtIdx(avatar, nextTileId, false);

            await _roomGrain.AvatarModule.NotifyWalkOnAsync(avatar, nextTileId, ct);

            avatar.RemoveStatus(AvatarStatusType.Lay, AvatarStatusType.Sit);
            avatar.AddStatus(AvatarStatusType.Move, $"{nextX},{nextY},{nextHeight}");
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

            await _roomGrain.AvatarModule.StopWalkingAsync(avatar, ct);
        }
    }
}
