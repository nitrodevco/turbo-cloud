using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class GetBadgesMessageHandler(IGrainFactory grainFactory) : IMessageHandler<GetBadgesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetBadgesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerBadgeGrain(ctx.PlayerId)
            .SendBadgeInventoryAsync(ct)
            .ConfigureAwait(false);
    }
}
