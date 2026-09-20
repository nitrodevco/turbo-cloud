using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// The club centre's renewal button for a non-VIP offer. See
/// <see cref="PurchaseVipMembershipExtensionMessageHandler"/> for why the two are the same.
/// </summary>
public class PurchaseBasicMembershipExtensionMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PurchaseBasicMembershipExtensionMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PurchaseBasicMembershipExtensionMessage message,
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
