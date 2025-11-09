using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

public class ExtendRentOrBuyoutFurniMessageHandler : IMessageHandler<ExtendRentOrBuyoutFurniMessage>
{
    public async ValueTask HandleAsync(
        ExtendRentOrBuyoutFurniMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
