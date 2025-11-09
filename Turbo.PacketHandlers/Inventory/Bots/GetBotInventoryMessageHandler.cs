using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Bots;

namespace Turbo.PacketHandlers.Inventory.Bots;

public class GetBotInventoryMessageHandler : IMessageHandler<GetBotInventoryMessage>
{
    public async ValueTask HandleAsync(
        GetBotInventoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
