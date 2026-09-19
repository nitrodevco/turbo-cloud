using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class SetActivatedBadgesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetActivatedBadgesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetActivatedBadgesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .SetActivatedBadgesAsync([.. message.BadgeCodes], ct)
            .ConfigureAwait(false);
    }
}
