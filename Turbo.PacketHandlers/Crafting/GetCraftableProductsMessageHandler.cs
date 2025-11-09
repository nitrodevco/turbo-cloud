using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

public class GetCraftableProductsMessageHandler : IMessageHandler<GetCraftableProductsMessage>
{
    public async ValueTask HandleAsync(
        GetCraftableProductsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
