using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
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

    public async Task<bool> LookToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        try
        {
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
