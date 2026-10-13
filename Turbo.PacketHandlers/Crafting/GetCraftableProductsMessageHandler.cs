using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

/// <summary>The crafting widget opened on a table (Flash CraftingWidgetHandler.as:132).</summary>
public class GetCraftableProductsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetCraftableProductsMessage>
{
    public async ValueTask HandleAsync(
        GetCraftableProductsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                grainFactory,
                message.ObjectId,
                new GetCraftableProductsInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
