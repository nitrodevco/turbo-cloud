using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Consumes the request until room invites are implemented: the messenger has no method to
/// deliver an invite to a friend yet.
/// </summary>
public class SendRoomInviteMessageHandler : IMessageHandler<SendRoomInviteMessage>
{
    public async ValueTask HandleAsync(
        SendRoomInviteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
