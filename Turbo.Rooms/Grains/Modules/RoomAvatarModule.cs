using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomAvatarModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private int _nextObjectId = 0;

    public async Task<IRoomAvatar> CreateAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        var objectId = GetNextObjectId();
        var startX = _roomGrain._state.Model?.DoorX ?? 0;
        var startY = _roomGrain._state.Model?.DoorY ?? 0;
        var startRot = _roomGrain._state.Model?.DoorRotation ?? Rotation.North;

        if (!_roomGrain.MapModule.InBounds(startX, startY))
        {
            // TODO get a valid tile
            startX = 0;
            startY = 0;
            startRot = Rotation.North;
        }

        var avatar = _roomGrain._avatarProvider.CreateAvatarFromPlayerSnapshot(objectId, snapshot);

        avatar.NextTileId = _roomGrain.MapModule.ToIdx(startX, startY);

        await _roomGrain.ObjectModule.AttatchObjectAsync(avatar, ct);

        _roomGrain._state.AvatarsByPlayerId[snapshot.PlayerId] = avatar.ObjectId;

        avatar.SetRotation(startRot);

        await LoadBadgesAsync(avatar, ct);

        return avatar;
    }

    /// <summary>The badges a player wears feed the "wearing badge" wired condition.</summary>
    private async Task LoadBadgesAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar is not IRoomPlayer player)
            return;

        try
        {
            var badges = await _roomGrain
                ._grainFactory.GetInventoryGrain(player.PlayerId)
                .GetSelectedBadgesAsync(ct);

            player.SetBadges([.. badges.Select(x => x.BadgeCode)]);
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Could not load the badges of player {PlayerId} entering room {RoomId}",
                player.PlayerId,
                _roomGrain.RoomId
            );
        }
    }

    /// <summary>
    /// A player in the room changed what they wear: the wired condition reads the new codes, and
    /// everyone in the room (the wearer too) sees them on the info stand.
    /// </summary>
    public Task SetPlayerBadgesAsync(
        PlayerId playerId,
        ImmutableArray<PlayerBadgeSnapshot> selectedBadges,
        CancellationToken ct
    )
    {
        if (!TryGetPlayer(playerId, out var player))
            return Task.CompletedTask;

        player.SetBadges([.. selectedBadges.Select(x => x.BadgeCode)]);

        return _roomGrain.SendComposerToRoomAsync(
            new HabboUserBadgesMessageComposer { PlayerId = playerId, Badges = selectedBadges },
            ct
        );
    }

    public async Task RemoveAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            if (!TryGetPlayer(playerId, out var avatar))
                return;

            await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, avatar, ct, -1);

            _roomGrain._state.AvatarsByPlayerId.Remove(playerId);
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Failed to remove the avatar of player {PlayerId} from room {RoomId}",
                playerId,
                _roomGrain.RoomId
            );
        }
    }

    /// <summary>
    /// The avatar of a player who is in the room. This is the one lookup from player id to
    /// avatar; modules, systems and wired boxes all come here instead of walking
    /// <c>AvatarsByPlayerId</c> and <c>AvatarsByObjectId</c> themselves.
    /// </summary>
    internal bool TryGetPlayer(PlayerId playerId, out IRoomPlayer player)
    {
        player = null!;

        if (
            playerId <= 0
            || !_roomGrain._state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
            || avatar is not IRoomPlayer roomPlayer
        )
            return false;

        player = roomPlayer;

        return true;
    }

    public async Task<bool> WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || !TryGetPlayer(ctx.PlayerId, out var avatar)
            || !await WalkAvatarToAsync(avatar, targetX, targetY, ct)
        )
            return false;

        return true;
    }

    public async Task<bool> WalkAvatarToAsync(
        RoomObjectId objectId,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (
            !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
            || !await WalkAvatarToAsync(avatar, targetX, targetY, ct)
        )
            return false;

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
            if (avatar.IsFrozen)
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            var goalTileId = _roomGrain.MapModule.ToIdx(targetX, targetY);
            var currentTileId =
                avatar.NextTileId > 0
                    ? avatar.NextTileId
                    : _roomGrain.MapModule.ToIdx(avatar.X, avatar.Y);
            var (currentX, currentY) = _roomGrain.MapModule.GetTileXY(currentTileId);

            if ((goalTileId == currentTileId) || !avatar.SetGoalTileId(goalTileId))
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            var path = _roomGrain.PathingSystem.FindPath(
                avatar,
                (currentX, currentY),
                (targetX, targetY)
            );

            if (path.Count == 0)
                throw new TurboException(TurboErrorCodeEnum.InvalidMoveTarget);

            avatar.TilePath.Clear();
            avatar.TilePath.AddRange(
                path.Skip(1).Select(pos => _roomGrain.MapModule.ToIdx(pos.X, pos.Y))
            );

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
            _roomGrain
                ._state.AvatarsByObjectId.Values.Select(x => x.GetSnapshot())
                .ToImmutableArray()
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
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Failed to stop the avatar {ObjectId} in room {RoomId}",
                avatar.ObjectId,
                _roomGrain.RoomId
            );
        }
    }

    public async Task ProcessNextAvatarStepAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        try
        {
            var nextTileId = avatar.NextTileId;

            if (nextTileId < 0)
                return;

            avatar.NextTileId = -1;

            var prevTileId = _roomGrain.MapModule.ToIdx(avatar.X, avatar.Y);
            var (nextX, nextY) = _roomGrain.MapModule.GetTileXY(nextTileId);

            if (prevTileId == nextTileId)
                return;

            _roomGrain.MapModule.RemoveAvatar(avatar, false);

            avatar.SetPosition(nextX, nextY);

            _roomGrain.MapModule.AddAvatar(avatar, false);
            _roomGrain.MapModule.UpdateHeightForAvatar(avatar);
        }
        catch (Exception)
        {
            await StopWalkingAsync(avatar, ct);
        }
    }

    public Task<bool> UpdateAvatarWithPlayerAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        if (
            snapshot.PlayerId <= 0
            || !TryGetPlayer(snapshot.PlayerId, out var avatarPlayer)
            || !avatarPlayer.UpdateWithPlayer(snapshot)
        )
            return Task.FromResult(false);

        _roomGrain
            .SendComposerToRoomAsync(
                new UserChangeMessageComposer
                {
                    ObjectId = avatarPlayer.ObjectId,
                    Figure = avatarPlayer.Figure,
                    Gender = avatarPlayer.Gender,
                    CustomInfo = avatarPlayer.Motto,
                    AchievementScore = snapshot.AchievementScore,
                    BadgesRank = snapshot.BadgesRank,
                },
                ct
            )
            .LogAndForget(_roomGrain._logger, $"send a composer to room {_roomGrain.RoomId}");

        return Task.FromResult(true);
    }

    public Task<bool> SetAvatarDanceAsync(
        RoomObjectId objectId,
        AvatarDanceType danceType,
        CancellationToken ct
    )
    {
        if (
            objectId <= 0
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar)
            || avatar is not IRoomPlayer player
            || !player.SetDance(danceType)
        )
            return Task.FromResult(false);

        _roomGrain
            .SendComposerToRoomAsync(
                new DanceMessageComposer
                {
                    ObjectId = avatar.ObjectId,
                    DanceType = player.DanceType,
                },
                ct
            )
            .LogAndForget(_roomGrain._logger, $"send a composer to room {_roomGrain.RoomId}");

        PublishAction(player, WiredAvatarActionType.Dance, (int)player.DanceType);

        return Task.FromResult(true);
    }

    public Task<bool> SetAvatarEffectAsync(
        RoomObjectId objectId,
        int effectId,
        CancellationToken ct
    )
    {
        if (
            objectId <= 0
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar)
            || avatar is not IRoomPlayer player
            || !player.SetEffect(effectId)
        )
            return Task.FromResult(false);

        _roomGrain
            .SendComposerToRoomAsync(
                new AvatarEffectMessageComposer
                {
                    ObjectId = avatar.ObjectId,
                    EffectId = player.EffectId,
                    DelayMilliseconds = 0,
                },
                ct
            )
            .LogAndForget(_roomGrain._logger, $"send a composer to room {_roomGrain.RoomId}");

        return Task.FromResult(true);
    }

    public Task<bool> SetAvatarExpressionAsync(
        RoomObjectId objectId,
        AvatarExpressionType expressionType,
        CancellationToken ct
    )
    {
        if (
            objectId <= 0
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar)
        )
            return Task.FromResult(false);

        _roomGrain
            .SendComposerToRoomAsync(
                new ExpressionMessageComposer
                {
                    ObjectId = avatar.ObjectId,
                    ExpressionType = expressionType,
                },
                ct
            )
            .LogAndForget(_roomGrain._logger, $"send a composer to room {_roomGrain.RoomId}");

        if (avatar is IRoomPlayer expressingPlayer)
            PublishAction(expressingPlayer, ToActionType(expressionType), 0);

        return Task.FromResult(true);
    }

    public Task<bool> SetAvatarSignAsync(RoomObjectId objectId, int signType, CancellationToken ct)
    {
        if (
            objectId <= 0
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar)
        )
            return Task.FromResult(false);

        avatar.AddStatus(AvatarStatusType.Sign, signType.ToString());

        if (avatar is IRoomPlayer signingPlayer)
            PublishAction(signingPlayer, WiredAvatarActionType.Sign, signType);

        return Task.FromResult(true);
    }

    public Task<bool> LookToAsync(ActionContext ctx, int targetX, int targetY, CancellationToken ct)
    {
        if (
            !_roomGrain.MapModule.InBounds(targetX, targetY)
            || !TryGetPlayer(ctx.PlayerId, out var avatar)
        )
            return Task.FromResult(false);

        // Turning mid-walk would fight the next step's rotation; the walk already faces its path.
        if (avatar.IsWalking || (avatar.X == targetX && avatar.Y == targetY))
            return Task.FromResult(false);

        var rotation = RotationExtensions.FromPoints(avatar.X, avatar.Y, targetX, targetY);

        avatar.SetBodyRotation(rotation);
        avatar.SetHeadRotation(rotation);
        avatar.MarkDirty();

        return Task.FromResult(true);
    }

    /// <summary>
    /// Marks the avatar active. An idle avatar wakes up, which the room sees as a sleep update.
    /// </summary>
    public void TouchAvatar(PlayerId playerId, long nowMs)
    {
        if (!TryGetPlayer(playerId, out var avatar))
            return;

        avatar.Touch(nowMs);

        if (!avatar.IsIdle)
            return;

        avatar.SetIdle(false);

        _roomGrain
            .SendComposerToRoomAsync(
                new SleepMessageComposer { UserId = avatar.ObjectId, IsSleeping = false },
                CancellationToken.None
            )
            .LogAndForget(_roomGrain._logger, $"send a composer to room {_roomGrain.RoomId}");
    }

    public Task SetHandItemAsync(IRoomAvatar avatar, int handItemId, CancellationToken ct)
    {
        if (!avatar.SetHandItem(handItemId))
            return Task.CompletedTask;

        if (handItemId > 0)
            _roomGrain.TimerSystem.Schedule(
                avatar.ObjectId,
                _roomGrain._roomConfig.HandItemExpireMs,
                _ => SetHandItemAsync(avatar, 0, CancellationToken.None)
            );
        else
            _roomGrain.TimerSystem.Cancel(avatar.ObjectId);

        return _roomGrain.SendComposerToRoomAsync(
            new CarryObjectMessageComposer { UserId = avatar.ObjectId, ItemType = handItemId },
            ct
        );
    }

    public async Task<bool> PassHandItemAsync(
        ActionContext ctx,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (
            targetId <= 0
            || targetId == ctx.PlayerId
            || !TryGetPlayer(ctx.PlayerId, out var giver)
            || !TryGetPlayer(targetId, out var receiver)
        )
            return false;

        var handItemId = giver.HandItemId;

        // Passing needs the two avatars side by side, as the client only offers it then.
        if (
            handItemId <= 0
            || Math.Max(Math.Abs(giver.X - receiver.X), Math.Abs(giver.Y - receiver.Y)) > 1
        )
            return false;

        await SetHandItemAsync(giver, 0, ct);
        await SetHandItemAsync(receiver, handItemId, ct);

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            targetId,
            new HandItemReceivedMessageComposer
            {
                GiverPlayerId = ctx.PlayerId,
                HandItemType = handItemId,
            },
            ct
        );

        return true;
    }

    public async Task<bool> DropHandItemAsync(ActionContext ctx, CancellationToken ct)
    {
        if (!TryGetPlayer(ctx.PlayerId, out var avatar) || avatar.HandItemId <= 0)
            return false;

        await SetHandItemAsync(avatar, 0, ct);

        return true;
    }

    public Task<bool> SetAvatarPostureAsync(
        RoomObjectId objectId,
        AvatarPostureType postureType,
        CancellationToken ct
    )
    {
        if (
            objectId <= 0
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar)
        )
            return Task.FromResult(false);

        switch (postureType)
        {
            case AvatarPostureType.Sit:
                avatar.Sit(true);
                if (avatar is IRoomPlayer sittingPlayer)
                    PublishAction(sittingPlayer, WiredAvatarActionType.Sit, 0);
                break;
            case AvatarPostureType.Stand:
                avatar.Sit(false);
                if (avatar is IRoomPlayer standingPlayer)
                    PublishAction(standingPlayer, WiredAvatarActionType.Stand, 0);
                break;
        }

        return Task.FromResult(true);
    }

    private static WiredAvatarActionType ToActionType(AvatarExpressionType expressionType) =>
        expressionType switch
        {
            AvatarExpressionType.Wave => WiredAvatarActionType.Wave,
            AvatarExpressionType.Blow => WiredAvatarActionType.Blow,
            AvatarExpressionType.Laugh => WiredAvatarActionType.Laugh,
            AvatarExpressionType.Respect => WiredAvatarActionType.Respect,
            AvatarExpressionType.Idle => WiredAvatarActionType.Sleep,
            AvatarExpressionType.Jump => WiredAvatarActionType.Jump,
            _ => WiredAvatarActionType.Wave,
        };

    /// <summary>Feeds the "performs action" wired trigger. Queued, so it never blocks the caller.</summary>
    private void PublishAction(IRoomPlayer player, WiredAvatarActionType actionType, int value) =>
        _roomGrain
            .PublishRoomEventAsync(
                new PlayerPerformsActionEvent
                {
                    RoomId = _roomGrain.RoomId,
                    CausedBy = ActionContext.CreateForPlayer(player.PlayerId, _roomGrain.RoomId),
                    PlayerId = player.PlayerId,
                    ActionType = actionType,
                    Value = value,
                },
                CancellationToken.None
            )
            .LogAndForget(_roomGrain._logger, $"publish an event in room {_roomGrain.RoomId}");

    internal int GetNextObjectId()
    {
        var objectId = _nextObjectId += 1;

        return objectId;
    }
}
