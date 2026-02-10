using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;

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

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        await messengerGrain
            .SetRelationshipStatusAsync(message.FriendUserId, message.RelationType, ct)
            .ConfigureAwait(false);
    }
}
