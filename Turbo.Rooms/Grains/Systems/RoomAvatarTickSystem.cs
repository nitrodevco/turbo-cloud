using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
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

        foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
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

                if (!avatar.IsDirty)
                    continue;

                dirtySnapshots.Add(avatar.GetSnapshot());
            }
            catch (Exception)
            {
                continue;
            }
        }

        if (dirtySnapshots.Count == 0)
            return;

        _ = _roomGrain.SendComposerToRoomAsync(
            new UserUpdateMessageComposer { Avatars = [.. dirtySnapshots] }
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

            if (Math.Abs(nextHeight - prevHeight) > Math.Abs(_roomGrain._roomConfig.MaxStepHeight))
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

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

                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);
            }

            var prevHighestItemId = _roomGrain._state.TileHighestFloorItems[prevTileId];
            var nextHighestItemId = _roomGrain._state.TileHighestFloorItems[nextTileId];

            if (
                prevHighestItemId > 0
                && _roomGrain._state.FloorItemsById.TryGetValue(
                    prevHighestItemId,
                    out var prevFloorItem
                )
            )
            {
                await prevFloorItem.Logic.OnWalkOffAsync(
                    (IRoomAvatarContext)avatar.Logic.Context,
                    ct
                );
            }

            _roomGrain.MapModule.RemoveAvatarAtIdx(avatar, prevTileId, false);
            _roomGrain.MapModule.AddAvatarAtIdx(avatar, nextTileId, false);

            if (
                nextHighestItemId > 0
                && _roomGrain._state.FloorItemsById.TryGetValue(
                    nextHighestItemId,
                    out var nextFloorItem
                )
            )
            {
                await nextFloorItem.Logic.OnWalkOnAsync(
                    (IRoomAvatarContext)avatar.Logic.Context,
                    ct
                );
            }

            avatar.RemoveStatus(AvatarStatusType.Lay, AvatarStatusType.Sit);
            avatar.AddStatus(AvatarStatusType.Move, $"{nextX},{nextY},{nextHeight}");
            avatar.SetRotation(RotationExtensions.FromPoints(avatar.X, avatar.Y, nextX, nextY));

            avatar.NextTileId = nextTileId;
        }
        catch (Exception)
        {
            await _roomGrain.AvatarModule.StopWalkingAsync(avatar, ct);
        }
    }
}
