using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// The club centre's renewal button. The client picks between this and the basic one by the
/// offer's <c>vip</c> flag; both buy the same offer the same way, because this hotel has one
/// Habbo Club tier.
/// </summary>
public class PurchaseVipMembershipExtensionMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PurchaseVipMembershipExtensionMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PurchaseVipMembershipExtensionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.PurchaseOfferAsync(
                _grainFactory,
                CatalogType.Normal,
                message.OfferId,
                string.Empty,
                1,
                ct
            )
            .ConfigureAwait(false);
    }
}
