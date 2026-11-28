using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Networking.Features;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Pets;

/// <summary>
/// The pets tab. A client that named <see cref="ClientFeatures.PET_INVENTORY_LOCATIONS"/> in its
/// hello lists its pets standing in rooms too, each with its room; any other gets the pets in the
/// inventory, as it always did.
/// </summary>
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
        var inventory = _grainFactory.GetInventoryGrain(ctx.PlayerId);

        if (ctx.ClientFeatures.Contains(ClientFeatures.PET_INVENTORY_LOCATIONS))
            await inventory.SendPetInventoryWithLocationsAsync(ct).ConfigureAwait(false);
        else
            await inventory.SendPetInventoryAsync(ct).ConfigureAwait(false);
    }
}
