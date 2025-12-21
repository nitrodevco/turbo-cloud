using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
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
            var avatar = await _avatarModule.CreateAvatarFromPlayerAsync(ctx, snapshot, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                $"Failed to create avatar for player {snapshot.PlayerId} in room {_roomId}."
            );

            return false;
        }
    }

    public async Task<bool> RemoveAvatarFromPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        try
        {
            await _avatarModule.RemoveAvatarFromPlayerAsync(playerId, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                $"Failed to remove avatar for player {playerId} in room {_roomId}."
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
            if (!await _avatarModule.WalkAvatarToAsync(ctx, targetX, targetY, ct))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                $"Failed to walk avatar for player {ctx.PlayerId} in room {_roomId} to ({targetX}, {targetY})."
            );

            return false;
        }
    }

    public Task<ImmutableArray<RoomAvatarSnapshot>> GetAllAvatarSnapshotsAsync(
        CancellationToken ct
    ) => _avatarModule.GetAllAvatarSnapshotsAsync(ct);
}
