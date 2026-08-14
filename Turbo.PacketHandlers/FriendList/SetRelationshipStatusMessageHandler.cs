using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.PacketHandlers.FriendList;

public class SetRelationshipStatusMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetRelationshipStatusMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetRelationshipStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .SetRelationshipStatusAsync(
                message.FriendUserId,
                (MessengerFriendRelationType)message.RelationType,
                ct
            )
            .ConfigureAwait(false);
    }
}
