using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Notifications;

namespace Turbo.PacketHandlers.Notifications;

public class ResetUnseenItemsMessageHandler : IMessageHandler<ResetUnseenItemsMessage>
{
    public async ValueTask HandleAsync(
        ResetUnseenItemsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
