using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class ConfirmAcceptTradingMessageHandler : IMessageHandler<ConfirmAcceptTradingMessage>
{
    public async ValueTask HandleAsync(
        ConfirmAcceptTradingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
