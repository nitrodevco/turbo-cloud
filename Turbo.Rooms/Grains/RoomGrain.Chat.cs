using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> SendChatFromPlayerAsync(
        ActionContext ctx,
        RoomChatType chatType,
        string text,
        int styleId,
        CancellationToken ct,
        int trackingId = -1,
        string? recipientName = null
    )
    {
        try
        {
            return await ChatSystem.SendChatFromPlayerAsync(
                ctx,
                chatType,
                text,
                styleId,
                trackingId,
                recipientName,
                ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send {ChatType} from player {PlayerId} in room {RoomId}",
                chatType,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<bool> SetAvatarTypingAsync(
        ActionContext ctx,
        bool isTyping,
        CancellationToken ct
    )
    {
        try
        {
            return await ChatSystem.SetAvatarTypingAsync(ctx, isTyping);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set typing:{IsTyping} for player {PlayerId} in room {RoomId}",
                isTyping,
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }
}
