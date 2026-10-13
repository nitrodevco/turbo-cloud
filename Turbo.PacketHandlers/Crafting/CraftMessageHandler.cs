using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

/// <summary>Crafts a listed recipe on a table (Flash CraftingWidgetHandler.doCraftingWithRecipe).</summary>
public class CraftMessageHandler(IGrainFactory grainFactory) : IMessageHandler<CraftMessage>
{
    public async ValueTask HandleAsync(
        CraftMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                grainFactory,
                message.ObjectId,
                new CraftInteraction { RecipeCode = message.RecipeCode },
                ct
            )
            .ConfigureAwait(false);
    }
}
