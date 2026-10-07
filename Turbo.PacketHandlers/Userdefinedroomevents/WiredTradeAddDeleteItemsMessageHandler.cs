using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>Offers or takes back inventory items in the player's trade with wired.</summary>
public class WiredTradeAddDeleteItemsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredTradeAddDeleteItemsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredTradeAddDeleteItemsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.ItemIds.IsDefaultOrEmpty)
            return;

        var trade = _grainFactory.GetWiredTradeGrain(ctx.PlayerId);
        var itemIds = message
            .ItemIds.Where(id => id > 0)
            .Select(RoomObjectId.Parse)
            .ToImmutableArray();

        if (message.IsDelete)
            await trade.RemoveItemsAsync(itemIds, ct).ConfigureAwait(false);
        else
            await trade.AddItemsAsync(itemIds, ct).ConfigureAwait(false);
    }
}
