using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class AddItemToTradeMessageHandler : IMessageHandler<AddItemToTradeMessage>
{
    public async ValueTask HandleAsync(
        AddItemToTradeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
