using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> MutePlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        int durationMinutes,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.MutePlayerAsync(ctx, playerId, durationMinutes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to mute player {TargetId} for {Minutes}m by {PlayerId} in room {RoomId}",
                playerId,
                durationMinutes,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> UnmutePlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.UnmutePlayerAsync(ctx, playerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to unmute player {TargetId} by {PlayerId} in room {RoomId}",
                playerId,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> ToggleRoomMuteAsync(ActionContext ctx, CancellationToken ct)
    {
        try
        {
            return await ModerationModule.ToggleRoomMuteAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to toggle room mute by {PlayerId} in room {RoomId}",
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public Task<bool> GetIsRoomMutedAsync(CancellationToken ct) =>
        Task.FromResult(_state.IsRoomMuted);
}
