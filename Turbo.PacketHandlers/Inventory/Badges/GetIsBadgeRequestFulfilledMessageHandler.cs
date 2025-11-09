using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class GetIsBadgeRequestFulfilledMessageHandler
    : IMessageHandler<GetIsBadgeRequestFulfilledMessage>
{
    public async ValueTask HandleAsync(
        GetIsBadgeRequestFulfilledMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
