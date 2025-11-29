using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums;
using Turbo.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Avatars;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomAvatarModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule,
    IRoomAvatarFactory roomAvatarFactory,
    IRoomObjectLogicFactory objectLogicFactory
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly IRoomAvatarFactory _roomAvatarFactory = roomAvatarFactory;
    private readonly IRoomObjectLogicFactory _objectLogicFactory = objectLogicFactory;

    private int _nextObjectId = 0;

    private void InvokeAvatar(RoomObjectId objectId)
    {
        if (!_state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar))
            throw new InvalidOperationException("Avatar not found in room state.");

        var current = _roomMap.GetTileIdForAvatar(avatar);
        var currentFlags = _state.TileFlags[current];

        if (!currentFlags.Has(RoomTileFlags.Sittable) || !currentFlags.Has(RoomTileFlags.Layable))
        {
            // disable sitting/laying if tile doesn't support it
        }
    }

    public async Task<IRoomAvatar> CreateAvatarFromPlayerAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct = default
    )
    {
        _nextObjectId += 1;

        var avatar = _roomAvatarFactory.CreateAvatarFromPlayerSnapshot(
            objectId: RoomObjectId.From(_nextObjectId),
            snapshot: snapshot
        );

        await AddAvatarAsync(avatar, ct);

        var startX = _state.Model?.DoorX ?? 0;
        var startY = _state.Model?.DoorY ?? 0;
        var startRot = _state.Model?.DoorRotation ?? 0;
        var tileId = -1;

        try
        {
            tileId = _roomMap.GetTileId(startX, startY);
        }
        catch (Exception)
        {
            startX = 0;
            startY = 0;
            startRot = 0;
        }

        avatar.SetPosition(x: startX, y: startY, z: 0.0, rot: startRot, headRot: startRot);

        await StopWalkingAsync(avatar, ct);

        _ = _roomGrain.SendComposerToRoomAsync(
            new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] },
            ct
        );

        return avatar;
    }

    private async Task<IRoomAvatar> AddAvatarAsync(
        IRoomAvatar avatar,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(avatar);

        if (!_state.AvatarsByObjectId.TryAdd(avatar.ObjectId.Value, avatar))
            throw new InvalidOperationException("Failed to add avatar to room state.");

        void func(RoomObjectId objectId) => _state.DirtyAvatarIds.Add(objectId.Value);

        avatar.SetAction(func);

        await AttatchLogicIfNeededAsync(avatar, ct);

        _state.AvatarMovementsByObjectId[avatar.ObjectId.Value] = RoomAvatarMovementState.From(
            avatar.ObjectId,
            avatar.X,
            avatar.Y
        );

        return avatar;
    }

    public async Task RemoveAvatarAsync(RoomObjectId objectId, CancellationToken ct = default)
    {
        if (objectId.IsEmpty())
            return;

        var avatar = _state.AvatarsByObjectId[objectId.Value];

        if (avatar is not null)
        {
            await StopWalkingAsync(avatar, ct);

            await avatar.Logic.OnDetachAsync(ct).ConfigureAwait(false);
        }

        _state.AvatarMovementsByObjectId.Remove(objectId.Value);

        // remove from avatar tile stack
    }

    private async Task AttatchLogicIfNeededAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar.Logic is not null)
            return;

        var logicType = "default_avatar";
        var ctx = new RoomAvatarContext(_roomGrain, this, avatar);
        var logic = _objectLogicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IMovingAvatarLogic avatarLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        avatar.SetLogic(avatarLogic);

        await avatarLogic.OnAttachAsync(ct);
    }

    public async Task WalkAvatarToAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int targetX,
        int targetY,
        CancellationToken ct = default
    )
    {
        if (!_state.AvatarMovementsByObjectId.TryGetValue(objectId.Value, out var moveState))
            return;

        if (!_state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar))
            return;

        try
        {
            await ProcessNextAvatarStepAsync(avatar, ct);

            var targetTileId = _roomMap.GetTileId(targetX, targetY);

            if (
                (avatar.X == targetX && avatar.Y == targetY)
                || !_roomMap.CheckIfTileValidForAvatar(avatar, targetTileId)
            )
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            moveState.GoalTileId = targetTileId;
            moveState.TilePath.Clear();
            moveState.TilePath.Enqueue(targetTileId);
            moveState.IsWalking = true;
        }
        catch (Exception e)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }

    public async Task ProcessNextAvatarStepAsync(IRoomAvatar avatar, CancellationToken ct = default)
    {
        if (!_state.AvatarMovementsByObjectId.TryGetValue(avatar.ObjectId.Value, out var moveState))
            return;

        try
        {
            if (_roomMap.IsTileInBounds(moveState.NextTileId))
            {
                moveState.PrevTileId = _roomMap.GetTileId(avatar.X, avatar.Y);

                var (nextX, nextY) = _roomMap.GetTileXY(moveState.NextTileId);

                avatar.SetPosition(
                    x: nextX,
                    y: nextY,
                    z: avatar.Z,
                    rot: avatar.Rotation,
                    headRot: avatar.HeadRotation
                );
            }

            moveState.NextTileId = -1;

            await UpdateHeightForAvatarAsync(avatar, ct);
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }

    public async Task StopWalkingAsync(IRoomAvatar avatar, CancellationToken ct = default)
    {
        if (
            !_state.AvatarMovementsByObjectId.TryGetValue(avatar.ObjectId.Value, out var moveState)
            || !moveState.IsWalking
        )
            return;

        moveState.TilePath.Clear();
        moveState.NextTileId = -1;
        moveState.GoalTileId = -1;
        moveState.IsWalking = false;

        avatar.RemoveStatus(RoomAvatarStatusType.Move);

        // invoke / stop on tile
        await UpdateHeightForAvatarAsync(avatar, ct);
    }

    public async Task<double> GetTileHeightForAvatarAsync(
        int tileId,
        CancellationToken ct = default
    )
    {
        try
        {
            var height = _state.TileHeights[tileId];
            var flags = _state.TileFlags[tileId];
            var highestItemId = _state.TileHighestFloorItems[tileId];

            if (
                highestItemId > 0
                && (flags.Has(RoomTileFlags.Sittable) || flags.Has(RoomTileFlags.Layable))
            )
            {
                var floorItem = _state.FloorItemsById[highestItemId];

                if (floorItem is not null)
                {
                    height -= floorItem.Definition.StackHeight;
                }
            }

            return height;
        }
        catch (Exception)
        {
            return 0.0;
        }
    }

    public async Task UpdateHeightForAvatarAsync(IRoomAvatar avatar, CancellationToken ct = default)
    {
        var tileId = _roomMap.GetTileId(avatar.X, avatar.Y);
        var prev = avatar.Z;
        var next = await GetTileHeightForAvatarAsync(tileId, ct);

        if (prev == next)
            return;

        avatar.SetPosition(avatar.X, avatar.Y, next, avatar.Rotation, avatar.HeadRotation);
    }

    internal async Task FlushDirtyAvatarsAsync(CancellationToken ct)
    {
        var avatars = _state.AvatarsByObjectId.Values.ToArray();

        if (avatars.Length == 0)
            return;

        var dirtySnapshots = new List<RoomAvatarSnapshot>();

        foreach (var avatar in avatars)
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
                // ignore
            }
        }

        if (dirtySnapshots.Count == 0)
            return;

        _ = _roomGrain.SendComposerToRoomAsync(
            new UserUpdateMessageComposer { Avatars = [.. dirtySnapshots] },
            ct
        );
    }

    internal async Task ProcessDirtyAvatarAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        await ProcessNextAvatarStepAsync(avatar, ct);

        if (
            !_state.AvatarMovementsByObjectId.TryGetValue(avatar.ObjectId.Value, out var moveState)
            || !moveState.IsWalking
        )
            return;

        if (moveState.TilePath.Count > 0)
        {
            var nextTileId = moveState.TilePath.Dequeue();

            if (nextTileId > 0)
                await ProcessAvatarStepAsync(avatar, nextTileId, ct);

            return;
        }

        await StopWalkingAsync(avatar, ct);
    }

    private async Task ProcessAvatarStepAsync(IRoomAvatar avatar, int tileId, CancellationToken ct)
    {
        if (!_state.AvatarMovementsByObjectId.TryGetValue(avatar.ObjectId.Value, out var moveState))
            return;

        try
        {
            var isGoal = moveState.TilePath.Count == 0;
            var prevTileId = _roomMap.GetTileId(avatar.X, avatar.Y);
            var nextTileId = tileId;
            var (nextX, nextY) = _roomMap.GetTileXY(nextTileId);
            var prevHeight = await GetTileHeightForAvatarAsync(prevTileId, ct);
            var nextHeight = await GetTileHeightForAvatarAsync(nextTileId, ct);

            if (Math.Abs(nextHeight - prevHeight) > Math.Abs(_roomConfig.MaxStepHeight))
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            var nextTileFlags = _state.TileFlags[nextTileId];
            var nextAvatarStack = _state.TileAvatarStacks[nextTileId];

            if (isGoal)
            {
                if (
                    nextTileFlags.Has(RoomTileFlags.Closed)
                    || (
                        nextTileFlags.Has(RoomTileFlags.AvatarOccupied)
                        && !nextAvatarStack.Contains(avatar.ObjectId.Value)
                    )
                )
                    throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);
            }
            else
            {
                if (
                    nextTileFlags.Has(RoomTileFlags.AvatarOccupied)
                    && !nextAvatarStack.Contains(avatar.ObjectId.Value)
                )
                {
                    // if blocking is allowed, recompile the path
                }
            }

            var prevHighestObjectId = _state.TileHighestFloorItems[prevTileId];
            var nextHighestObjectId = _state.TileHighestFloorItems[nextTileId];

            if (prevHighestObjectId != nextHighestObjectId && prevHighestObjectId > 0)
            {
                // leave the prev
            }

            _state.TileAvatarStacks[prevTileId].Remove(avatar.ObjectId.Value);
            _state.TileAvatarStacks[nextTileId].Add(avatar.ObjectId.Value);

            avatar.RemoveStatus(RoomAvatarStatusType.Lay, RoomAvatarStatusType.Sit);
            avatar.AddStatus(RoomAvatarStatusType.Move, $"{nextX},{nextY},{nextHeight}");
            // set the rotation towards next tile

            moveState.NextTileId = nextTileId;

            if (nextHighestObjectId > 0)
            {
                // enter the next
            }
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }
}
