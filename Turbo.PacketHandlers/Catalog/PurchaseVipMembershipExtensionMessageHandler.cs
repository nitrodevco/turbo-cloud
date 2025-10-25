using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class PurchaseVipMembershipExtensionMessageHandler
    : IMessageHandler<PurchaseVipMembershipExtensionMessage>
{
    public async ValueTask HandleAsync(
        PurchaseVipMembershipExtensionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
