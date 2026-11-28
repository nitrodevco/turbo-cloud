using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Pets;

/// <summary>
/// Asked once when the client's pets tab initialises; it offers "send to retirement" only after
/// the inventory has answered with the price.
/// </summary>
public class GetPetRetirementPriceMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetPetRetirementPriceMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetPetRetirementPriceMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .SendPetRetirementPriceAsync(ct)
            .ConfigureAwait(false);
    }
}
