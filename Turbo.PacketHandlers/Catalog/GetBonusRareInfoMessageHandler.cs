using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetBonusRareInfoMessageHandler(IBonusRareProvider bonusRareProvider)
    : IMessageHandler<GetBonusRareInfoMessage>
{
    private readonly IBonusRareProvider _bonusRareProvider = bonusRareProvider;

    public async ValueTask HandleAsync(
        GetBonusRareInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var info = await _bonusRareProvider.GetInfoAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new BonusRareInfoMessageComposer
                {
                    ProductType = info.ProductCode,
                    ProductClassId = info.ProductClassId,
                    TotalCoinsForBonus = info.TotalCoinsForBonus,
                    CoinsStillRequiredToBuy = info.CoinsStillRequiredToBuy,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
