using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Messages.Outgoing.Notifications;

namespace Turbo.PacketHandlers.Inventory.Purse;

public class GetCreditsInfoMessageHandler : IMessageHandler<GetCreditsInfoMessage>
{
    public async ValueTask HandleAsync(
        GetCreditsInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(new CreditBalanceEventMessageComposer { Balance = "0" }, ct)
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new ActivityPointsMessageComposer
                {
                    PointsByCategoryId = ImmutableDictionary<int, int>.Empty,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
