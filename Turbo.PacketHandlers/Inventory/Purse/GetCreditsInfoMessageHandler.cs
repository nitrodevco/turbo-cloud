using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;

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

        var wallet = _grainFactory.GetPlayerWalletGrain(ctx.PlayerId);
        var credits = await wallet
            .GetAmountForCurrencyAsync(new CurrencyKind { CurrencyType = CurrencyType.Credits }, ct)
            .ConfigureAwait(false);
        var activityPoints = await wallet.GetActivityPointsAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new CreditBalanceEventMessageComposer { Balance = $"{credits}.0" },
                ct
            )
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new ActivityPointsMessageComposer { PointsByCategoryId = activityPoints },
                ct
            )
            .ConfigureAwait(false);
    }
}
