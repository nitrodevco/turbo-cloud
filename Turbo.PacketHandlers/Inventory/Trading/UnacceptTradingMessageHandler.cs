using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;

namespace Turbo.PacketHandlers.Inventory.Trading;

public class UnacceptTradingMessageHandler : IMessageHandler<UnacceptTradingMessage>
{
    public async ValueTask HandleAsync(
        UnacceptTradingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
