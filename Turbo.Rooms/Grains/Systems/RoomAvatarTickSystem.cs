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
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Grains.Systems;

internal sealed class RoomAvatarTickSystem(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomAvatarModule roomAvatarModule,
    RoomMapModule roomMapModule
)
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomAvatarModule _roomAvatar = roomAvatarModule;
    private readonly RoomMapModule _roomMap = roomMapModule;

    public async Task ProcessAvatarsAsync(CancellationToken ct)
    {
        var dirtySnapshots = new List<RoomAvatarSnapshot>();

        foreach (var avatar in _state.AvatarsByObjectId.Values)
        {
            try
            {
                await ProcessDirtyAvatarAsync(avatar, ct);

                if (!avatar.IsDirty)
                    continue;

                var snapshot = avatar.GetSnapshot();

                dirtySnapshots.Add(snapshot);
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

    internal async Task ProcessDirtyAvatarAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar.NeedsInvoke)
            await _roomMap.InvokeAvatarAsync(avatar, ct);

        await _roomAvatar.ProcessNextAvatarStepAsync(avatar, ct);

        if (avatar.TilePath.Count <= 0)
        {
            await _roomAvatar.StopWalkingAsync(avatar, ct);

            return;
        }

        var nextTileId = avatar.TilePath[0];

        avatar.TilePath.RemoveAt(0);

        await ValidateAvatarStepAsync(avatar, nextTileId, ct);
    }

    internal async Task ValidateAvatarStepAsync(
        IRoomAvatar avatar,
        int nextTileId,
        CancellationToken ct
    )
    {
        try
        {
            var isGoal = avatar.TilePath.Count == 0;
            var prevTileId = _roomMap.ToIdx(avatar.X, avatar.Y);
            var (nextX, nextY) = _roomMap.GetTileXY(nextTileId);
            var prevHeight = _roomMap.GetTileHeightForAvatar(prevTileId);
            var nextHeight = _roomMap.GetTileHeightForAvatar(nextTileId);

            if (Math.Abs(nextHeight - prevHeight) > Math.Abs(_roomConfig.MaxStepHeight))
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            if (!_roomMap.CanAvatarWalkBetween(avatar, prevTileId, nextTileId, isGoal))
            {
                if (!isGoal)
                {
                    var (goalX, goalY) = _roomMap.GetTileXY(avatar.GoalTileId);

                    await _roomAvatar.WalkAvatarToAsync(avatar, goalX, goalY, ct);
                    await ProcessDirtyAvatarAsync(avatar, ct);

                    return;
                }

                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);
            }

            _roomMap.RemoveAvatar(avatar, false);
            _roomMap.AddAvatar(avatar, false);

            avatar.RemoveStatus(AvatarStatusType.Lay, AvatarStatusType.Sit);
            avatar.AddStatus(AvatarStatusType.Move, $"{nextX},{nextY},{nextHeight}");
            avatar.SetRotation(RotationExtensions.FromPoints(avatar.X, avatar.Y, nextX, nextY));
            avatar.SetNextTileId(nextTileId);
        }
        catch (Exception)
        {
            await _roomAvatar.StopWalkingAsync(avatar, ct);
        }
    }
}
