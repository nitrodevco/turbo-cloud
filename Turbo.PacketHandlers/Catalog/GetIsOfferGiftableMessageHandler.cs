using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// Left unanswered on purpose. The client has no message that answers it: whether an offer can
/// be gifted travels with the offer on its catalog page, and the Flash client's only sender of
/// this packet (<c>HabboCatalog.checkGiftable</c>) is never called. A gift purchase is checked
/// again when it is made.
/// </summary>
public class GetIsOfferGiftableMessageHandler : IMessageHandler<GetIsOfferGiftableMessage>
{
    public async ValueTask HandleAsync(
        GetIsOfferGiftableMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
