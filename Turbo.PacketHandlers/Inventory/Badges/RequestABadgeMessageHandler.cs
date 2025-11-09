using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class RequestABadgeMessageHandler : IMessageHandler<RequestABadgeMessage>
{
    public async ValueTask HandleAsync(
        RequestABadgeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
