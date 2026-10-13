using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Crafting;

/// <summary>What a listed recipe takes (Flash CraftingWidgetHandler.getCraftingRecipe: the code only).</summary>
public class GetCraftingRecipeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetCraftingRecipeMessage>
{
    public async ValueTask HandleAsync(
        GetCraftingRecipeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || string.IsNullOrEmpty(message.RecipeCode))
            return;

        await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SendCraftingRecipeAsync(ctx.AsActionContext(), message.RecipeCode, ct)
            .ConfigureAwait(false);
    }
}
