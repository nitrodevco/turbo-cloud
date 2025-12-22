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

    public async Task ProcessAvatarsAsync(long now, CancellationToken ct)
    {
        var dirtySnapshots = new List<RoomAvatarSnapshot>();

        foreach (var avatar in _state.AvatarsByObjectId.Values)
        {
            try
            {
                await _roomAvatar.ProcessNextAvatarStepAsync(avatar, ct);

                if (avatar.TilePath.Count <= 0)
                {
                    if (avatar.PendingStopAtMs > 0 && now < avatar.PendingStopAtMs)
                        continue;

                    await _roomAvatar.StopWalkingAsync(avatar, ct);

                    if (avatar.NeedsInvoke)
                        await _roomMap.InvokeAvatarAsync(avatar, ct);
                }
                else
                {
                    var nextTileId = avatar.TilePath[0];
                    avatar.TilePath.RemoveAt(0);

                    if (avatar.TilePath.Count == 0)
                        avatar.PendingStopAtMs = _roomGrain.AlignToNextBoundary(
                            now,
                            _roomConfig.AvatarTickMs
                        );

                    await ValidateAvatarStepAsync(avatar, nextTileId, now, ct);
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

                    if (await _roomAvatar.WalkAvatarToAsync(avatar, goalX, goalY, ct))
                    {
                        nextTileId = avatar.TilePath[0];
                        avatar.TilePath.RemoveAt(0);

                        await ValidateAvatarStepAsync(avatar, nextTileId, now, ct);

                        return;
                    }
                }

                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);
            }

            _roomMap.RemoveAvatarAtIdx(avatar, prevTileId, false);
            _roomMap.AddAvatarAtIdx(avatar, nextTileId, false);

            avatar.RemoveStatus(AvatarStatusType.Lay, AvatarStatusType.Sit);
            avatar.AddStatus(AvatarStatusType.Move, $"{nextX},{nextY},{nextHeight}");
            avatar.SetRotation(RotationExtensions.FromPoints(avatar.X, avatar.Y, nextX, nextY));

            avatar.NextTileId = nextTileId;
        }
        catch (Exception)
        {
            await _roomAvatar.StopWalkingAsync(avatar, ct);
        }
    }
}
