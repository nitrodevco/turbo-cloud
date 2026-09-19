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

        // The list is fetched here and handed to the presence, which only sends it: the presence
        // must not call the inventory, because the inventory awaits the presence when it changes.
        var badges = await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .GetAllBadgeSnapshotsAsync(ct)
            .ConfigureAwait(false);

        await _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .SendBadgeInventoryAsync(badges, ct)
            .ConfigureAwait(false);
    }
}
