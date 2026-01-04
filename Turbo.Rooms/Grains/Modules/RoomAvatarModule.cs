using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomAvatarModule(
    RoomGrain roomGrain,
    RoomLiveState roomLiveState,
    RoomPathingSystem roomPathing,
    RoomSecurityModule securityModule,
    RoomMapModule roomMapModule
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomSecurityModule _securityModule = securityModule;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly RoomPathingSystem _pathingSystem = roomPathing;

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

        if (!_roomMap.InBounds(startX, startY))
        {
            // TODO get a valid tile
            startX = 0;
            startY = 0;
            startRot = Rotation.North;
        }

        var avatar = _roomGrain._roomAvatarFactory.CreateAvatarFromPlayerSnapshot(
            objectId,
            snapshot
        );

        var controllerLevel = await _securityModule.GetControllerLevelAsync(ctx);

        avatar.AddStatus(AvatarStatusType.FlatControl, ((int)controllerLevel).ToString());

        avatar.NextTileId = _roomMap.ToIdx(startX, startY);

        await AddAvatarAsync(avatar, ct);

        _state.AvatarsByPlayerId[snapshot.PlayerId] = avatar.ObjectId;

        avatar.SetRotation(startRot);

        return avatar;
    }

    private async Task<IRoomAvatar> AddAvatarAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(avatar);

        if (!_state.AvatarsByObjectId.TryAdd(avatar.ObjectId, avatar))
            throw new TurboException(TurboErrorCodeEnum.AvatarNotFound);

        await AttatchLogicIfNeededAsync(avatar, ct);
        await ProcessNextAvatarStepAsync(avatar, ct);
        await _roomGrain.SendComposerToRoomAsync(
            new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] }
        );

        return avatar;
    }

    public async Task RemoveAvatarAsync(RoomObjectId objectId, CancellationToken ct)
    {
        try
        {
            if (!_state.AvatarsByObjectId.TryGetValue(objectId, out var avatar))
                return;

            await StopWalkingAsync(avatar, ct);
            await avatar.Logic.OnDetachAsync(ct);

            _roomMap.RemoveAvatar(avatar, false);

            _state.AvatarsByObjectId.Remove(objectId);

            await _roomGrain.SendComposerToRoomAsync(
                new UserRemoveMessageComposer { ObjectId = objectId }
            );
        }
        catch (Exception) { }
    }

    public async Task RemoveAvatarFromPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        try
        {
            if (!_state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId))
                return;

            await RemoveAvatarAsync(objectId, ct);

            _state.AvatarsByPlayerId.Remove(playerId);
        }
        catch (Exception) { }
    }

    private async Task AttatchLogicIfNeededAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar.Logic is not null)
            return;

        var logicType = "default_avatar";
        var ctx = new RoomAvatarContext(_roomGrain, this, avatar);
        var logic = _roomGrain._logicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IRoomAvatarLogic avatarLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        avatar.SetLogic(avatarLogic);

        await avatarLogic.OnAttachAsync(ct);
    }

    public async Task<bool> WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return false;

        if (!_state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectIdValue))
            return false;

        if (!_state.AvatarsByObjectId.TryGetValue(objectIdValue, out var avatar))
            return false;

        await WalkAvatarToAsync(avatar, targetX, targetY, ct);

        return true;
    }

    public async Task<bool> WalkAvatarToAsync(
        RoomObjectId objectId,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (!_state.AvatarsByObjectId.TryGetValue(objectId, out var avatar))
            return false;

        await WalkAvatarToAsync(avatar, targetX, targetY, ct);

        return true;
    }

    public async Task<bool> WalkAvatarToAsync(
        IRoomAvatar avatar,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        try
        {
            var goalTileId = _roomMap.ToIdx(targetX, targetY);
            var currentTileId =
                avatar.NextTileId > 0 ? avatar.NextTileId : _roomMap.ToIdx(avatar.X, avatar.Y);
            var (currentX, currentY) = _roomMap.GetTileXY(currentTileId);

            if ((goalTileId == currentTileId) || !avatar.SetGoalTileId(goalTileId))
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            var path = _pathingSystem.FindPath(
                avatar,
                _roomMap,
                (currentX, currentY),
                (targetX, targetY)
            );

            if (path.Count == 0)
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            avatar.TilePath.Clear();
            avatar.TilePath.AddRange(path.Skip(1).Select(pos => _roomMap.ToIdx(pos.X, pos.Y)));

            avatar.IsWalking = true;

            return true;
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);

            return false;
        }
    }

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.AvatarsByObjectId.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    public async Task StopWalkingAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        try
        {
            if (!avatar.IsWalking)
                return;

            avatar.IsWalking = false;
            avatar.NextMoveStepAtMs = 0;
            avatar.NextMoveUpdateAtMs = 0;
            avatar.PendingStopAtMs = 0;

            await ProcessNextAvatarStepAsync(avatar, ct);

            avatar.TilePath.Clear();
            avatar.NextTileId = -1;
            avatar.SetGoalTileId(-1);
            avatar.RemoveStatus(AvatarStatusType.Move);
            avatar.NeedsInvoke = true;
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

            var prevTileId = _roomMap.ToIdx(avatar.X, avatar.Y);
            var (nextX, nextY) = _roomMap.GetTileXY(nextTileId);

            if (prevTileId == nextTileId)
                return;

            _roomMap.RemoveAvatar(avatar, false);

            avatar.SetPosition(nextX, nextY);

            _roomMap.AddAvatar(avatar, false);

            _roomMap.UpdateHeightForAvatar(avatar);
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }
}
