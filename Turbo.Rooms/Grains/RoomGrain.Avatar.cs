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
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> CreateAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerSummarySnapshot snapshot,
        RoomEntrySnapshot entry,
        CancellationToken ct
    )
    {
        try
        {
            var avatar = await AvatarModule.CreateAvatarFromPlayerAsync(ctx, snapshot, entry, ct);

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

    public Task SetPlayerBadgesAsync(
        PlayerId playerId,
        ImmutableArray<PlayerBadgeSnapshot> selectedBadges,
        CancellationToken ct
    ) => AvatarModule.SetPlayerBadgesAsync(playerId, selectedBadges, ct);

    public Task SetPlayerHabboClubAsync(
        PlayerId playerId,
        DateTime? expiresAt,
        CancellationToken ct
    ) => AvatarModule.SetPlayerHabboClubAsync(playerId, expiresAt, ct);

    public async Task<bool> RemoveAvatarFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            // The trade grain calls this room back to clear the status, so it is told, never
            // awaited. A token of its own: the caller's may be cancelled once we return.
            if (TradeModule.IsTrading(playerId))
                _grainFactory
                    .GetRoomTradeGrain(_state.RoomId)
                    .CloseForPlayerAsync(playerId, CancellationToken.None)
                    .LogAndForget(
                        _logger,
                        $"close the trade of player {playerId} leaving room {_state.RoomId}"
                    );

            // Read before the avatar goes: what left is told with the event, because by then
            // there is nothing left to look up.
            var objectId = AvatarModule.TryGetPlayer(playerId, out var leaving)
                ? leaving.ObjectId
                : RoomObjectId.Parse(0);

            await AvatarModule.RemoveAvatarFromPlayerAsync(ctx, playerId, ct);

            await PublishRoomEventAsync(
                new PlayerLeftEvent
                {
                    RoomId = _state.RoomId,
                    CausedBy = ctx,
                    PlayerId = playerId,
                    ObjectId = objectId,
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

    public Task<bool> SetAvatarDanceAsync(
        ActionContext ctx,
        AvatarDanceType danceType,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "dance",
            danceType,
            async () =>
                AvatarModule.TryGetPlayer(ctx.PlayerId, out var avatar)
                && await AvatarModule.SetAvatarDanceAsync(avatar.ObjectId, danceType, ct)
        );

    public Task<bool> SetAvatarEffectAsync(ActionContext ctx, int effectId, CancellationToken ct) =>
        RunLoggedAsync(
            ctx,
            "wear effect",
            effectId,
            async () =>
                AvatarModule.TryGetPlayer(ctx.PlayerId, out var avatar)
                && await AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, effectId, ct)
        );

    public Task<bool> SetAvatarExpressionAsync(
        ActionContext ctx,
        AvatarExpressionType expressionType,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "show expression",
            expressionType,
            async () =>
                AvatarModule.TryGetPlayer(ctx.PlayerId, out var avatar)
                && await AvatarModule.SetAvatarExpressionAsync(avatar.ObjectId, expressionType, ct)
        );

    public Task<bool> SetAvatarSignAsync(ActionContext ctx, int signType, CancellationToken ct) =>
        RunLoggedAsync(
            ctx,
            "show sign",
            signType,
            async () =>
                AvatarModule.TryGetPlayer(ctx.PlayerId, out var avatar)
                && await AvatarModule.SetAvatarSignAsync(avatar.ObjectId, signType, ct)
        );

    public Task<bool> SetAvatarPostureAsync(
        ActionContext ctx,
        AvatarPostureType postureType,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "take posture",
            postureType,
            async () =>
                AvatarModule.TryGetPlayer(ctx.PlayerId, out var avatar)
                && await AvatarModule.SetAvatarPostureAsync(avatar.ObjectId, postureType, ct)
        );

    public Task<bool> PassHandItemAsync(
        ActionContext ctx,
        PlayerId targetId,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "pass a hand item to player",
            targetId,
            () => AvatarModule.PassHandItemAsync(ctx, targetId, ct)
        );

    public Task<bool> DropHandItemAsync(ActionContext ctx, CancellationToken ct) =>
        RunLoggedAsync(
            ctx,
            "drop",
            "their hand item",
            () => AvatarModule.DropHandItemAsync(ctx, ct)
        );

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
                || !AvatarModule.TryGetAvatar(targetObjectId, out _)
                || !AvatarModule.TryGetPlayer(ctx.PlayerId, out _)
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
                || !AvatarModule.TryGetPlayer(targetId, out _)
                || !AvatarModule.TryGetPlayer(ctx.PlayerId, out _)
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

    public Task<bool> LookToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "turn towards",
            (targetX, targetY),
            () => AvatarModule.LookToAsync(ctx, targetX, targetY, ct)
        );

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    ) => AvatarModule.GetAllAvatarSnapshotsAsync(ct);
}
