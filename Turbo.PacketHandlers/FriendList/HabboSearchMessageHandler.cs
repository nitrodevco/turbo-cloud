using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class HabboSearchMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<HabboSearchMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        HabboSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var (friends, others) = await messengerGrain
            .SearchPlayersAsync(message.SearchQuery, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new HabboSearchResultMessageComposer { Friends = friends, Others = others },
                ct
            )
            .ConfigureAwait(false);
    }
}
