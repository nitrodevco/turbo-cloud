using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// The offer dialog's buy (<c>OfferController.purchaseTargetedOffer</c>): the client counts the
/// quantity off its own limit and docks or closes the window without waiting for an answer, so
/// none is sent beyond what the grant itself tells (the new furni, the purse).
/// </summary>
public class PurchaseTargetedOfferMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PurchaseTargetedOfferMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PurchaseTargetedOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerTargetedOfferGrain(ctx.PlayerId)
            .PurchaseAsync(message.OfferId, message.Quantity, ct)
            .ConfigureAwait(false);
    }
}
