using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;

namespace Turbo.PacketHandlers.Inventory.Furni;

public class RequestRoomPropertySetMessageHandler : IMessageHandler<RequestRoomPropertySetMessage>
{
    public async ValueTask HandleAsync(
        RequestRoomPropertySetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
