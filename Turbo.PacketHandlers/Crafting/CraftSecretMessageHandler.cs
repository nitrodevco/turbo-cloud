using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

/// <summary>Crafts what the mixer's items make (Flash CraftingWidgetHandler.doCraftingWithMixer).</summary>
public class CraftSecretMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CraftSecretMessage>
{
    public async ValueTask HandleAsync(
        CraftSecretMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                grainFactory,
                message.ObjectId,
                new CraftingMixerInteraction { ItemIds = message.ItemIds, Craft = true },
                ct
            )
            .ConfigureAwait(false);
    }
}
