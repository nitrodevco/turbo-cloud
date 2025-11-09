using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;

namespace Turbo.PacketHandlers.Room.Action;

public class RemoveAllRightsMessageHandler : IMessageHandler<RemoveAllRightsMessage>
{
    public async ValueTask HandleAsync(
        RemoveAllRightsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
