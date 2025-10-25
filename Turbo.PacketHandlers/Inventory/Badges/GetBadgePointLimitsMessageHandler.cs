using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class GetBadgePointLimitsMessageHandler : IMessageHandler<GetBadgePointLimitsMessage>
{
    public async ValueTask HandleAsync(
        GetBadgePointLimitsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
