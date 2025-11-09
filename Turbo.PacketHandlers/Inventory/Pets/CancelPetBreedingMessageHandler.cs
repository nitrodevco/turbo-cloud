using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;

namespace Turbo.PacketHandlers.Inventory.Pets;

public class CancelPetBreedingMessageHandler : IMessageHandler<CancelPetBreedingMessage>
{
    public async ValueTask HandleAsync(
        CancelPetBreedingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
