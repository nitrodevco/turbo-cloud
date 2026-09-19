using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Pets;

public class GetPetInventoryMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetPetInventoryMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetPetInventoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .OpenPetInventoryAsync(ct)
            .ConfigureAwait(false);
    }
}
