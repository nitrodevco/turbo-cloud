using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
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
}
