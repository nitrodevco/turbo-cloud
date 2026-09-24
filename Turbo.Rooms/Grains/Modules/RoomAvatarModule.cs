using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomAvatarModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private int _nextObjectId = 0;

    public async Task<IRoomAvatar> CreateAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerSummarySnapshot snapshot,
        RoomEntrySnapshot entry,
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

        avatar.SetRoomEntry(entry);

        avatar.NextTileId = _roomGrain.MapModule.ToIdx(startX, startY);

        await _roomGrain.ObjectModule.AttatchObjectAsync(avatar, ct);

        _roomGrain._state.AvatarsByPlayerId[snapshot.PlayerId] = avatar.ObjectId;

        avatar.SetRotation(startRot);

        await LoadBadgesAsync(avatar, ct);
        await LoadHabboClubAsync(avatar, ct);
        await LoadFavouriteGuildAsync(avatar, ct);

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
    /// The Habbo Club membership a player is standing here with, which the wired
    /// <c>@is_hc</c> variable reads. Loaded on the way in, like the badges: putting it on
    /// <c>PlayerSummarySnapshot</c> would add a grain call to every hot path that asks for a
    /// summary.
    /// </summary>
    private async Task LoadHabboClubAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar is not IRoomPlayer player)
            return;

        try
        {
            var club = await _roomGrain
                ._grainFactory.GetPlayerSubscriptionGrain(player.PlayerId)
                .GetAsync(SubscriptionType.HabboClub, ct);

            player.SetHabboClubExpiresAt(club.ExpiresAt);
        }
        catch (Exception ex)
        {
            // A player whose membership could not be read is treated as holding none, which is
            // the safe way round: wired grants nothing it should not.
            _roomGrain._logger.LogWarning(
                ex,
                "Could not load the Habbo Club membership of player {PlayerId} entering room {RoomId}",
                player.PlayerId,
                _roomGrain.RoomId
            );
        }
    }

    /// <summary>
    /// The group badge a player walks in wearing. Loaded on the way in like the badges and the
    /// club: it feeds the two wired group boxes and the badge the client draws beside them, and
    /// putting it on <c>PlayerSummarySnapshot</c> would add a grain call to every hot path that
    /// asks for a summary.
    /// </summary>
    public async Task LoadFavouriteGuildAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar is not IRoomPlayer player)
            return;

        try
        {
            var guildId = await _roomGrain
                ._grainFactory.GetPlayerGuildGrain(player.PlayerId)
                .GetFavouriteGuildIdAsync(ct);

            if (guildId is not { } favourite)
            {
                player.SetFavouriteGuild(-1, -1, string.Empty);

                return;
            }

            var guild = await _roomGrain
                ._grainFactory.GetGuildDirectoryGrain()
                .GetSummaryAsync(favourite, ct);

            if (guild is null)
            {
                player.SetFavouriteGuild(-1, -1, string.Empty);

                return;
            }

            player.SetFavouriteGuild(guild.GuildId, (int)GuildMembershipStatus.Member, guild.Name);
        }
        catch (Exception ex)
        {
            // A player whose group could not be read wears none, which is the safe way round:
            // the wired group boxes grant nothing they should not.
            _roomGrain._logger.LogWarning(
                ex,
                "Could not load the favourite group of player {PlayerId} entering room {RoomId}",
                player.PlayerId,
                _roomGrain.RoomId
            );
        }
    }

    /// <summary>
    /// A player in the room bought or extended their Habbo Club. Nothing is drawn for it; it is
    /// what <c>@is_hc</c> answers from. An expiry needs no such call, because the value is the
    /// moment it runs out.
    /// </summary>
    public Task SetPlayerHabboClubAsync(
        PlayerId playerId,
        DateTime? expiresAt,
        CancellationToken ct
    )
    {
        if (TryGetPlayer(playerId, out var player))
            player.SetHabboClubExpiresAt(expiresAt);

        return Task.CompletedTask;
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
    // Read access for the systems that are not this module (wired above all). They ask here
    // instead of reading the room state, so how avatars are indexed can change in one place.

    /// <summary>Every avatar in the room: players, pets and bots.</summary>
    public IReadOnlyCollection<IRoomAvatar> Avatars => _roomGrain._state.AvatarsByObjectId.Values;

    public IEnumerable<IRoomPlayer> Players => Avatars.OfType<IRoomPlayer>();

    public bool TryGetAvatar(RoomObjectId objectId, out IRoomAvatar avatar)
    {
        if (_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var found))
        {
            avatar = found;

            return true;
        }

        avatar = null!;

        return false;
    }

    /// <summary>The avatars on a tile; none for a tile outside the room.</summary>
    public IEnumerable<IRoomAvatar> GetAvatarsOnTile(int tileIdx)
    {
        if (!_roomGrain.MapModule.InBounds(tileIdx))
            yield break;

        foreach (var objectId in _roomGrain._state.TileAvatarStacks[tileIdx])
        {
            if (_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar))
                yield return avatar;
        }
    }

    public bool HasAvatarOnTile(int tileIdx) =>
        _roomGrain.MapModule.InBounds(tileIdx)
        && _roomGrain._state.TileAvatarStacks[tileIdx].Count > 0;

    /// <summary>
    /// The avatars on any tile a floor item covers. With <paramref name="standingOnIt"/> only
    /// those the item actually carries count: the ones on a tile where it is the highest furni.
    /// </summary>
    public IEnumerable<IRoomAvatar> GetAvatarsOnItem(IRoomFloorItem item, bool standingOnIt = false)
    {
        if (!_roomGrain.FurniModule.GetTileIdForFloorItem(item, out var tileIds))
            yield break;

        foreach (var tileIdx in tileIds)
        {
            if (standingOnIt && !_roomGrain.FurniModule.IsHighestOnTile(item, tileIdx))
                continue;

            foreach (var avatar in GetAvatarsOnTile(tileIdx))
                yield return avatar;
        }
    }

    /// <summary>The player closest to a tile and no further than <paramref name="maxDistance"/>.</summary>
    public bool TryGetNearestPlayer(
        int tileIdx,
        int maxDistance,
        out IRoomPlayer nearest,
        out int distance
    )
    {
        nearest = null!;
        distance = int.MaxValue;

        var map = _roomGrain.MapModule;

        foreach (var player in Players)
        {
            var away = map.GetDistanceBetween(tileIdx, map.ToIdx(player.X, player.Y));

            if (away > maxDistance || away >= distance)
                continue;

            nearest = player;
            distance = away;
        }

        return nearest is not null;
    }

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

    /// <summary>
    /// Puts an avatar on a tile at once, without walking there: a teleport, or being carried or
    /// pushed. It is the one place that does it, so the furni left behind and the furni landed on
    /// always hear of it, as they do for a walked step. Whether the avatar may stand there is
    /// the caller's question (<see cref="RoomMapModule.CanAvatarWalk"/>); telling the room is
    /// the caller's too, since a wired move and a plain status update look different on the wire.
    /// </summary>
    public async Task RelocateAvatarAsync(IRoomAvatar avatar, int tileIdx, CancellationToken ct)
    {
        var map = _roomGrain.MapModule;
        var sourceIdx = map.ToIdx(avatar.X, avatar.Y);

        if (sourceIdx == tileIdx)
            return;

        await StopWalkingAsync(avatar, ct);
        await NotifyWalkOffAsync(avatar, sourceIdx, ct);

        var (targetX, targetY) = map.GetTileXY(tileIdx);

        map.RemoveAvatarAtIdx(avatar, sourceIdx, false);
        avatar.SetPosition(targetX, targetY);
        map.AddAvatarAtIdx(avatar, tileIdx, false);
        map.UpdateHeightForAvatar(avatar);

        avatar.RemoveStatus(AvatarStatusType.Move);
        avatar.NeedsInvoke = true;
        avatar.MarkDirty();

        await NotifyWalkOnAsync(avatar, tileIdx, ct);
    }

    /// <summary>Tells the furni an avatar stands on that the avatar is leaving it.</summary>
    public Task NotifyWalkOffAsync(IRoomAvatar avatar, int tileIdx, CancellationToken ct) =>
        TryGetWalkableItem(tileIdx, out var item)
            ? item.Logic.OnWalkOffAsync((IRoomAvatarContext)avatar.Logic.Context, ct)
            : Task.CompletedTask;

    /// <summary>Tells the furni on a tile that an avatar arrived on it.</summary>
    public Task NotifyWalkOnAsync(IRoomAvatar avatar, int tileIdx, CancellationToken ct) =>
        TryGetWalkableItem(tileIdx, out var item)
            ? item.Logic.OnWalkOnAsync((IRoomAvatarContext)avatar.Logic.Context, ct)
            : Task.CompletedTask;

    /// <summary>The furni an avatar on this tile stands on: the highest floor item, if any.</summary>
    private bool TryGetWalkableItem(int tileIdx, out IRoomFloorItem item)
    {
        item = null!;

        var itemId = _roomGrain._state.TileHighestFloorItems[tileIdx];

        if (
            itemId <= 0
            || !_roomGrain._state.ItemsById.TryGetValue(itemId, out var found)
            || found is not IRoomFloorItem floorItem
        )
            return false;

        item = floorItem;

        return true;
    }

    /// <summary>
    /// Furni that dresses a player (a mannequin, a clothing booth) changes their figure here.
    /// Told, never awaited: the player grain tells the presence, which comes back to this room
    /// to update the avatar, and a room waiting on that would be waiting on itself.
    /// </summary>
    public void ChangePlayerFigure(PlayerId playerId, string figure, AvatarGenderType gender) =>
        _roomGrain
            ._grainFactory.GetPlayerGrain(playerId)
            .SetFigureAsync(figure, gender, CancellationToken.None)
            .LogAndForget(
                _roomGrain._logger,
                $"change the figure of player {playerId} from room {_roomGrain.RoomId}"
            );

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
            || !avatar.SetDance(danceType)
        )
            return Task.FromResult(false);

        _roomGrain
            .SendComposerToRoomAsync(
                new DanceMessageComposer
                {
                    ObjectId = avatar.ObjectId,
                    DanceType = avatar.DanceType,
                },
                ct
            )
            .LogAndForget(_roomGrain._logger, $"send a composer to room {_roomGrain.RoomId}");

        PublishAction(avatar, AvatarActionType.Dance, (int)avatar.DanceType);

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
            || !avatar.SetEffect(effectId)
        )
            return Task.FromResult(false);

        _roomGrain
            .SendComposerToRoomAsync(
                new AvatarEffectMessageComposer
                {
                    ObjectId = avatar.ObjectId,
                    EffectId = avatar.EffectId,
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

        PublishAction(avatar, AvatarActionType.Expression, (int)expressionType);

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

        PublishAction(avatar, AvatarActionType.Sign, signType);

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
                new SleepMessageComposer { ObjectId = avatar.ObjectId, IsSleeping = false },
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
                PublishAction(avatar, AvatarActionType.Posture, (int)AvatarPostureType.Sit);
                break;
            case AvatarPostureType.Stand:
                avatar.Sit(false);
                PublishAction(avatar, AvatarActionType.Posture, (int)AvatarPostureType.Stand);
                break;
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Tells the room what an avatar just did. Queued, so it never blocks the caller. The event
    /// names the avatar by room index, so a bot or a pet is reported the same way a player is;
    /// only a player has a player id to blame it on, and anything else is the room's own doing.
    /// </summary>
    private void PublishAction(IRoomAvatar avatar, AvatarActionType actionType, int value) =>
        _roomGrain
            .PublishRoomEventAsync(
                new AvatarPerformsActionEvent
                {
                    RoomId = _roomGrain.RoomId,
                    CausedBy = avatar is IRoomPlayer player
                        ? ActionContext.CreateForPlayer(player.PlayerId, _roomGrain.RoomId)
                        : ActionContext.CreateForSystem(_roomGrain.RoomId),
                    ObjectId = avatar.ObjectId,
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
