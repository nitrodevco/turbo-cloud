using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
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
                $"Failed to create avatar for player {snapshot.PlayerId} in room {_state.RoomId}."
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
                $"Failed to remove avatar for player {playerId} in room {_state.RoomId}."
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
            if (!await AvatarModule.WalkAvatarToAsync(ctx, targetX, targetY, ct))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                $"Failed to walk avatar for player {ctx.PlayerId} in room {_state.RoomId} to ({targetX}, {targetY})."
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
                $"Failed to dance:{danceType} avatar for player {ctx.PlayerId} in room {_state.RoomId}"
            );

            return false;
        }
    }

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    ) => AvatarModule.GetAllAvatarSnapshotsAsync(ct);
}
