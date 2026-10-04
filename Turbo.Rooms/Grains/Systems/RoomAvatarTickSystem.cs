using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
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
        // The path is stored goal first, so the next step is the last entry.
        var nextTileId = avatar.TilePath[^1];
        avatar.TilePath.RemoveAt(avatar.TilePath.Count - 1);

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
            var prevTileId = MapModule.ToIdx(avatar.X, avatar.Y);
            var (nextX, nextY) = MapModule.GetTileXY(nextTileId);
            var prevHeight = MapModule.GetTileHeightForAvatar(prevTileId);
            var nextHeight = MapModule.GetTileHeightForAvatar(nextTileId);

            // A step the map refuses is the normal end of a walk, not a failure: the tile was
            // taken or raised while the avatar was on its way. Stop and say nothing; this runs
            // for every avatar on every tick.
            if (Math.Abs(nextHeight - prevHeight) > Math.Abs(_roomGrain._roomConfig.MaxStepHeight))
            {
                await AvatarModule.StopWalkingAsync(avatar, ct);

                return;
            }

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

            await AvatarModule.StopWalkingAsync(avatar, ct);
        }
    }
}
