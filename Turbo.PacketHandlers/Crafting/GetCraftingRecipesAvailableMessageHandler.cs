using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

/// <summary>The mixer's items changed (Flash CraftingWidget.mixerContentChanged): what do they fit?</summary>
public class GetCraftingRecipesAvailableMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetCraftingRecipesAvailableMessage>
{
    public async ValueTask HandleAsync(
        GetCraftingRecipesAvailableMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                grainFactory,
                message.ObjectId,
                new CraftingMixerInteraction { ItemIds = message.ItemIds, Craft = false },
                ct
            )
            .ConfigureAwait(false);
    }
}
