using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> SendChatFromPlayerAsync(
        ActionContext ctx,
        RoomChatType chatType,
        string text,
        int styleId,
        CancellationToken ct,
        int trackingId = -1,
        string? recipientName = null
    );
    public Task<bool> SetAvatarTypingAsync(ActionContext ctx, bool isTyping, CancellationToken ct);

    /// <summary>
    /// The room telling one player something, as a whisper over their avatar that nobody else
    /// sees: how an operator command answers from outside the room's turn. False when the player
    /// is not in this room.
    /// </summary>
    public Task<bool> WhisperToPlayerAsync(PlayerId playerId, string text, CancellationToken ct);
}
