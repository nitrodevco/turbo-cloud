using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> KickPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.KickPlayerAsync(ctx, playerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to kick player {TargetId} by {PlayerId} in room {RoomId}",
                playerId,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> BanPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomBanDurationType duration,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.BanPlayerAsync(ctx, playerId, duration, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to ban player {TargetId} for {Duration} by {PlayerId} in room {RoomId}",
                playerId,
                duration,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> UnbanPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.UnbanPlayerAsync(ctx, playerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to unban player {TargetId} by {PlayerId} in room {RoomId}",
                playerId,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<ImmutableArray<RoomBannedPlayerSnapshot>?> GetBannedPlayersAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.GetBannedPlayersAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to list bans of room {RoomId} for player {PlayerId}",
                _state.RoomId,
                ctx.PlayerId
            );

            return null;
        }
    }

    public async Task<ImmutableArray<string>?> GetRoomFilterWordsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.GetRoomFilterWordsAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to read the chat filter of room {RoomId} for player {PlayerId}",
                _state.RoomId,
                ctx.PlayerId
            );

            return null;
        }
    }

    public async Task<bool> UpdateRoomFilterAsync(
        ActionContext ctx,
        bool isAdding,
        string word,
        CancellationToken ct
    )
    {
        try
        {
            return await ModerationModule.UpdateRoomFilterAsync(ctx, isAdding, word, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update the chat filter of room {RoomId} for player {PlayerId}",
                _state.RoomId,
                ctx.PlayerId
            );

            return false;
        }
    }

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
