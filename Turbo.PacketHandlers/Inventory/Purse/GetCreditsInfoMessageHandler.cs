using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Purse;

public class GetCreditsInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetCreditsInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetCreditsInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var player = _grainFactory.GetPlayerGrain(ctx.PlayerId);
        var wallet = await player.GetWalletAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new CreditBalanceEventMessageComposer { Balance = $"{wallet.Credits}.0" },
                ct
            )
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new ActivityPointsMessageComposer
                {
                    PointsByCategoryId = wallet.ActivityPointsByCategoryId,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
