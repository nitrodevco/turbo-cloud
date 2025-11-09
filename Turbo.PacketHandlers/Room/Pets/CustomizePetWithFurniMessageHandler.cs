using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Pets;

namespace Turbo.PacketHandlers.Room.Pets;

public class CustomizePetWithFurniMessageHandler : IMessageHandler<CustomizePetWithFurniMessage>
{
    public async ValueTask HandleAsync(
        CustomizePetWithFurniMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
