using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class ConfirmDeclineTradingMessageHandler : IMessageHandler<ConfirmDeclineTradingMessage>
{
    public async ValueTask HandleAsync(
        ConfirmDeclineTradingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
