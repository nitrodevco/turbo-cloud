using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Crafting;

namespace Turbo.PacketHandlers.Crafting;

public class CraftMessageHandler : IMessageHandler<CraftMessage>
{
    public async ValueTask HandleAsync(
        CraftMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
