using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> CreateAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        try
        {
            var avatar = await AvatarModule.CreateAvatarFromPlayerAsync(ctx, snapshot, ct);

            await PublishRoomEventAsync(
                new PlayerEnterEvent
                {
                    RoomId = _state.RoomId,
                    CausedBy = ctx,
                    PlayerId = snapshot.PlayerId,
                },
                ct
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create avatar for player {PlayerId} in room {RoomId}.",
                snapshot.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> RemoveAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            await TradeModule.CloseForPlayerAsync(playerId, ct);
            await AvatarModule.RemoveAvatarFromPlayerAsync(ctx, playerId, ct);

            await PublishRoomEventAsync(
                new PlayerLeftEvent
                {
                    RoomId = _state.RoomId,
                    CausedBy = ctx,
                    PlayerId = playerId,
                },
                ct
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove avatar for player {PlayerId} in room {RoomId}.",
                playerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        try
        {
            return await AvatarModule.WalkAvatarToAsync(ctx, targetX, targetY, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to walk avatar for player {PlayerId} in room {RoomId} to ({TargetX}, {TargetY}).",
                ctx.PlayerId,
                _state.RoomId,
                targetX,
                targetY
            );

            return false;
        }
    }

    public async Task<bool> UpdateAvatarWithPlayerAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        try
        {
            return await AvatarModule.UpdateAvatarWithPlayerAsync(snapshot, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update avatar for player {PlayerId} in room {RoomId}",
                snapshot.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> SetAvatarDanceAsync(
        ActionContext ctx,
        AvatarDanceType danceType,
        CancellationToken ct
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            if (
                !_state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectId)
                || !await AvatarModule.SetAvatarDanceAsync(objectId, danceType, ct)
            )
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to dance:{DanceType} avatar for player {PlayerId} in room {RoomId}",
                danceType,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> SetAvatarEffectAsync(
        ActionContext ctx,
        int effectId,
        CancellationToken ct
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            if (
                !_state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectId)
                || !await AvatarModule.SetAvatarEffectAsync(objectId, effectId, ct)
            )
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to effect:{EffectId} avatar for player {PlayerId} in room {RoomId}",
                effectId,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> SetAvatarExpressionAsync(
        ActionContext ctx,
        AvatarExpressionType expressionType,
        CancellationToken ct
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            if (
                !_state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectId)
                || !await AvatarModule.SetAvatarExpressionAsync(objectId, expressionType, ct)
            )
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set expression:{ExpressionType} avatar for player {PlayerId} in room {RoomId}",
                expressionType,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> SetAvatarSignAsync(
        ActionContext ctx,
        int signType,
        CancellationToken ct
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            if (
                !_state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectId)
                || !await AvatarModule.SetAvatarSignAsync(objectId, signType, ct)
            )
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set sign:{SignType} avatar for player {PlayerId} in room {RoomId}",
                signType,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> SetAvatarPostureAsync(
        ActionContext ctx,
        AvatarPostureType postureType,
        CancellationToken ct
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            if (
                !_state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectId)
                || !await AvatarModule.SetAvatarPostureAsync(objectId, postureType, ct)
            )
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set posture:{PostureType} avatar for player {PlayerId} in room {RoomId}",
                postureType,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> PassHandItemAsync(
        ActionContext ctx,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            return await AvatarModule.PassHandItemAsync(ctx, targetId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to pass a hand item from player {PlayerId} to {TargetId} in room {RoomId}",
                ctx.PlayerId,
                targetId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> DropHandItemAsync(ActionContext ctx, CancellationToken ct)
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            return await AvatarModule.DropHandItemAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to drop the hand item of player {PlayerId} in room {RoomId}",
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> ClickAvatarAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    )
    {
        try
        {
            if (
                ctx.PlayerId <= 0
                || !_state.AvatarsByObjectId.ContainsKey(targetObjectId)
                || !_state.AvatarsByPlayerId.ContainsKey(ctx.PlayerId)
            )
                return false;

            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            await PublishRoomEventAsync(
                new PlayerClickedAvatarEvent
                {
                    RoomId = _state.RoomId,
                    CausedBy = ctx,
                    PlayerId = ctx.PlayerId,
                    TargetObjectId = targetObjectId,
                },
                ct
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Player {PlayerId} failed to click avatar {ObjectId} in room {RoomId}",
                ctx.PlayerId,
                targetObjectId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> RespectPlayerAsync(
        ActionContext ctx,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        try
        {
            if (
                targetId <= 0
                || targetId == ctx.PlayerId
                || !_state.AvatarsByPlayerId.ContainsKey(targetId)
                || !_state.AvatarsByPlayerId.ContainsKey(ctx.PlayerId)
            )
                return false;

            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            if (!await _grainFactory.GetPlayerGrain(ctx.PlayerId).TryUseRespectAsync(ct))
                return false;

            var total = await _grainFactory.GetPlayerGrain(targetId).ReceiveRespectAsync(ct);

            await SendComposerToRoomAsync(
                new RespectNotificationMessageComposer
                {
                    PlayerId = targetId,
                    RespectTotal = total,
                },
                ct
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to respect player {TargetId} from {PlayerId} in room {RoomId}",
                targetId,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> LookToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            return await AvatarModule.LookToAsync(ctx, targetX, targetY, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to turn avatar for player {PlayerId} in room {RoomId} towards ({TargetX}, {TargetY})",
                ctx.PlayerId,
                _state.RoomId,
                targetX,
                targetY
            );

            return false;
        }
    }

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    ) => AvatarModule.GetAllAvatarSnapshotsAsync(ct);
}
