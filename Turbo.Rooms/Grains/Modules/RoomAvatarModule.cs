using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Factories;
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
    RoomSecurityModule securityModule,
    RoomMapModule roomMapModule,
    RoomPathfinder roomPathfinder,
    IRoomAvatarFactory roomAvatarFactory,
    IRoomObjectLogicFactory objectLogicFactory
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomSecurityModule _securityModule = securityModule;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly RoomPathfinder _pathfinder = roomPathfinder;
    private readonly IRoomAvatarFactory _roomAvatarFactory = roomAvatarFactory;
    private readonly IRoomObjectLogicFactory _objectLogicFactory = objectLogicFactory;

    private int _nextObjectId = 0;

    public async Task<IRoomAvatar> CreateAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        var objectId = _nextObjectId += 1;
        var startX = _state.Model?.DoorX ?? 0;
        var startY = _state.Model?.DoorY ?? 0;
        var startRot = _state.Model?.DoorRotation ?? Rotation.North;

        if (!_roomMap.IsTileInBounds(startX, startY))
        {
            // TODO get a valid tile
            startX = 0;
            startY = 0;
            startRot = Rotation.North;
        }

        var avatar = _roomAvatarFactory.CreateAvatarFromPlayerSnapshot(
            RoomObjectId.From(objectId),
            snapshot
        );

        var controllerLevel = await _securityModule.GetControllerLevelAsync(ctx);

        avatar.AddStatus(AvatarStatusType.FlatControl, controllerLevel.ToString());

        avatar.NextTileId = _roomMap.GetTileId(startX, startY);

        await AddAvatarAsync(avatar, ct);

        _state.AvatarsByPlayerId[snapshot.PlayerId] = avatar.ObjectId.Value;

        avatar.SetRotation(startRot);

        return avatar;
    }

    private async Task<IRoomAvatar> AddAvatarAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(avatar);

        if (!_state.AvatarsByObjectId.TryAdd(avatar.ObjectId.Value, avatar))
            throw new InvalidOperationException("Failed to add avatar to room state.");

        await AttatchLogicIfNeededAsync(avatar, ct);
        await ProcessNextAvatarStepAsync(avatar, ct);

        _ = _roomGrain.SendComposerToRoomAsync(
            new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] },
            ct
        );

        return avatar;
    }

    public async Task RemoveAvatarFromPlayerAsync(long playerId, CancellationToken ct)
    {
        try
        {
            if (!_state.AvatarsByPlayerId.TryGetValue(playerId, out var objectIdValue))
                return;

            if (!_state.AvatarsByObjectId.TryGetValue(objectIdValue, out var avatar))
                return;

            await StopWalkingAsync(avatar, ct);

            var tileId = _roomMap.GetTileId(avatar.X, avatar.Y);

            if (_state.TileAvatarStacks[tileId].Remove(avatar.ObjectId.Value))
                await _roomMap.ComputeTileAsync(tileId);

            await avatar.Logic.OnDetachAsync(ct).ConfigureAwait(false);

            _state.AvatarsByPlayerId.Remove(objectIdValue);
            _state.AvatarsByObjectId.Remove(objectIdValue);

            _ = _roomGrain.SendComposerToRoomAsync(
                new UserRemoveMessageComposer { ObjectId = RoomObjectId.From(objectIdValue) },
                ct
            );
        }
        catch (Exception) { }
    }

    private async Task AttatchLogicIfNeededAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar.Logic is not null)
            return;

        var logicType = "default_avatar";
        var ctx = new RoomAvatarContext(_roomGrain, this, avatar);
        var logic = _objectLogicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IRoomAvatarLogic avatarLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        avatar.SetLogic(avatarLogic);

        await avatarLogic.OnAttachAsync(ct);
    }

    public async Task WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        if (!_state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectIdValue))
            return;

        if (!_state.AvatarsByObjectId.TryGetValue(objectIdValue, out var avatar))
            return;

        await WalkAvatarToAsync(avatar, targetX, targetY, ct);
    }

    public async Task WalkAvatarToAsync(
        RoomObjectId objectId,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (!_state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar))
            return;

        await WalkAvatarToAsync(avatar, targetX, targetY, ct);
    }

    public async Task WalkAvatarToAsync(
        IRoomAvatar avatar,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        try
        {
            await ProcessNextAvatarStepAsync(avatar, ct);

            var path = _pathfinder.FindPath(
                avatar,
                _roomMap,
                (avatar.X, avatar.Y),
                (targetX, targetY)
            );

            if (path.Count == 0)
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            avatar.TilePath.Clear();
            avatar.TilePath.AddRange(path.Skip(1).Select(pos => _roomMap.GetTileId(pos.X, pos.Y)));
            avatar.GoalTileId = _roomMap.GetTileId(targetX, targetY);
            avatar.IsWalking = true;
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.AvatarsByObjectId.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    private async Task InvokeAvatarAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        try
        {
            var tileId = _roomMap.GetTileId(avatar.X, avatar.Y);
            var highestItemId = _state.TileHighestFloorItems[tileId];

            if (highestItemId > 0)
            {
                var floorItem = _state.FloorItemsById[highestItemId];

                if (floorItem is not null)
                    await floorItem.Logic.OnStopAsync(avatar.Logic.Context, ct);
            }
        }
        catch (Exception) { }
    }

    private async Task StopWalkingAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        try
        {
            if (avatar.IsWalking)
            {
                avatar.IsWalking = false;

                await ProcessNextAvatarStepAsync(avatar, ct);

                avatar.TilePath.Clear();
                avatar.NextTileId = -1;
                avatar.GoalTileId = -1;

                avatar.RemoveStatus(AvatarStatusType.Move);

                await InvokeAvatarAsync(avatar, ct);
            }

            UpdateHeightForAvatar(avatar);
        }
        catch (Exception) { }
    }

    public async Task ProcessNextAvatarStepAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        try
        {
            var nextTileId = avatar.NextTileId;

            if (nextTileId < 0)
                return;

            avatar.NextTileId = -1;

            var prevTileId = _roomMap.GetTileId(avatar.X, avatar.Y);
            var (nextX, nextY) = _roomMap.GetTileXY(nextTileId);

            if (prevTileId == nextTileId)
                return;

            if (prevTileId > 0)
            {
                if (_state.TileAvatarStacks[prevTileId].Remove(avatar.ObjectId.Value))
                    await _roomMap.ComputeTileAsync(prevTileId);
            }

            if (!_state.TileAvatarStacks[nextTileId].Contains(avatar.ObjectId.Value))
            {
                _state.TileAvatarStacks[nextTileId].Add(avatar.ObjectId.Value);

                await _roomMap.ComputeTileAsync(nextTileId);
            }

            avatar.SetPosition(nextX, nextY);
            avatar.SetRotation(avatar.BodyRotation);

            UpdateHeightForAvatar(avatar);
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }

    private void UpdateHeightForAvatar(IRoomAvatar avatar)
    {
        var tileId = _roomMap.GetTileId(avatar.X, avatar.Y);
        var next = GetTileHeightForAvatar(tileId);

        avatar.SetHeight(next);
    }

    private double GetTileHeightForAvatar(int tileId)
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
                    height -= floorItem.Definition.StackHeight;
            }

            return height;
        }
        catch (Exception)
        {
            return 0.0;
        }
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
            catch (Exception) { }
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

        if (avatar.TilePath.Count <= 0)
        {
            await StopWalkingAsync(avatar, ct);

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
            var prevTileId = _roomMap.GetTileId(avatar.X, avatar.Y);
            var (nextX, nextY) = _roomMap.GetTileXY(nextTileId);
            var prevHeight = GetTileHeightForAvatar(prevTileId);
            var nextHeight = GetTileHeightForAvatar(nextTileId);

            if (Math.Abs(nextHeight - prevHeight) > Math.Abs(_roomConfig.MaxStepHeight))
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            if (!_roomMap.CanAvatarWalkBetween(avatar, prevTileId, nextTileId, isGoal))
            {
                if (!isGoal)
                {
                    var (goalX, goalY) = _roomMap.GetTileXY(avatar.GoalTileId);

                    await WalkAvatarToAsync(avatar, goalX, goalY, ct);
                    await ProcessDirtyAvatarAsync(avatar, ct);

                    return;
                }

                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);
            }

            _state.TileAvatarStacks[prevTileId].Remove(avatar.ObjectId.Value);

            await _roomMap.ComputeTileAsync(prevTileId);

            _state.TileAvatarStacks[nextTileId].Add(avatar.ObjectId.Value);

            await _roomMap.ComputeTileAsync(nextTileId);

            avatar.RemoveStatus(AvatarStatusType.Lay, AvatarStatusType.Sit);
            avatar.AddStatus(AvatarStatusType.Move, $"{nextX},{nextY},{nextHeight}");
            avatar.SetRotation(RotationExtensions.FromPoints(avatar.X, avatar.Y, nextX, nextY));

            avatar.NextTileId = nextTileId;
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }
}
