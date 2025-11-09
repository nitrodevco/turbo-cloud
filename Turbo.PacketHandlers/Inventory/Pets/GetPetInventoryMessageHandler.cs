using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;

namespace Turbo.PacketHandlers.Inventory.Pets;

public class GetPetInventoryMessageHandler : IMessageHandler<GetPetInventoryMessage>
{
    public async ValueTask HandleAsync(
        GetPetInventoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
