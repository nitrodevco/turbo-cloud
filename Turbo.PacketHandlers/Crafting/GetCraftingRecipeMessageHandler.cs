using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

public class GetCraftingRecipeMessageHandler : IMessageHandler<GetCraftingRecipeMessage>
{
    public async ValueTask HandleAsync(
        GetCraftingRecipeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
