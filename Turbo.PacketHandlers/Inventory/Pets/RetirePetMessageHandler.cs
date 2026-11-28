using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Pets;

/// <summary>
/// "Send to retirement" on a pet in the inventory. The inventory charges the price, deletes the
/// pet and answers with the outcome either way.
/// </summary>
public class RetirePetMessageHandler(IGrainFactory grainFactory) : IMessageHandler<RetirePetMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RetirePetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .RetirePetAsync(message.PetId, ct)
            .ConfigureAwait(false);
    }
}
