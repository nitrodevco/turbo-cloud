using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory;

namespace Turbo.PacketHandlers.Inventory.Purse;

public class GetCreditsInfoMessageHandler : IMessageHandler<GetCreditsInfoMessage>
{
    public async ValueTask HandleAsync(
        GetCreditsInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
