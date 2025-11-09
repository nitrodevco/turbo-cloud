using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;

namespace Turbo.PacketHandlers.Inventory.Pets;

public class ConfirmPetBreedingMessageHandler : IMessageHandler<ConfirmPetBreedingMessage>
{
    public async ValueTask HandleAsync(
        ConfirmPetBreedingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
