using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Sets what a friend is to the player (<c>RelationshipStatusSelector</c>). The work is <see
/// cref="IMessengerService"/>'s.
/// </summary>
public class SetRelationshipStatusMessageHandler(IMessengerService messengerService)
    : IMessageHandler<SetRelationshipStatusMessage>
{
    private readonly IMessengerService _messengerService = messengerService;

    public async ValueTask HandleAsync(
        SetRelationshipStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _messengerService
            .SetRelationshipStatusAsync(
                ctx.PlayerId,
                message.FriendUserId,
                message.RelationType,
                ct
            )
            .ConfigureAwait(false);
    }
}
