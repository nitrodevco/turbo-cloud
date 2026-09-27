using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Consumes the request until following a friend is implemented: nothing yet checks whether
/// the friend allows it and forwards the player to the friend's room.
/// </summary>
public class FollowFriendMessageHandler : IMessageHandler<FollowFriendMessage>
{
    public async ValueTask HandleAsync(
        FollowFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
