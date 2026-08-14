using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class AcceptFriendMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AcceptFriendMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AcceptFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var failures = await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .AcceptFriendRequestsAsync(message.Friends, ct)
            .ConfigureAwait(false);

        if (failures.Count == 0)
            return;

        await ctx.SendComposerAsync(
                new AcceptFriendResultMessageComposer { Failures = failures },
                ct
            )
            .ConfigureAwait(false);
    }
}
