using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Notifications;

namespace Turbo.PacketHandlers.Notifications;

public class ResetUnseenItemIdsMessageHandler : IMessageHandler<ResetUnseenItemIdsMessage>
{
    public async ValueTask HandleAsync(
        ResetUnseenItemIdsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
