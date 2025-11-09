using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

public class GetCraftingRecipesAvailableMessageHandler
    : IMessageHandler<GetCraftingRecipesAvailableMessage>
{
    public async ValueTask HandleAsync(
        GetCraftingRecipesAvailableMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
