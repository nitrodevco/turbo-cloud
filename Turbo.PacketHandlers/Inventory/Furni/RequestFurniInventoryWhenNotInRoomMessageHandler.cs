using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;

namespace Turbo.PacketHandlers.Inventory.Furni;

public class RequestFurniInventoryWhenNotInRoomMessageHandler
    : IMessageHandler<RequestFurniInventoryWhenNotInRoomMessage>
{
    public async ValueTask HandleAsync(
        RequestFurniInventoryWhenNotInRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
